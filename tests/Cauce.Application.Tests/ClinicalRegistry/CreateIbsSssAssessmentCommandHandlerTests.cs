using Cauce.Application.ClinicalRegistry.UseCases.CreateIbsSssAssessment;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Patients.UseCases.CompletePatientOnboarding;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas del handler <see cref="CreateIbsSssAssessmentCommandHandler"/>.
/// </summary>
public sealed class CreateIbsSssAssessmentCommandHandlerTests
{
    private const int PatientRoleId = 1;

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IIbsSssAssessmentRepository _assessmentRepository = Substitute.For<IIbsSssAssessmentRepository>();
    private readonly IIbsSssAssessmentScheduleRepository _scheduleRepository = Substitute.For<IIbsSssAssessmentScheduleRepository>();
    private readonly IOutboxWriter _outboxWriter = Substitute.For<IOutboxWriter>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISender _mediator = Substitute.For<ISender>();
    private readonly ILogger<CreateIbsSssAssessmentCommandHandler> _logger = Substitute.For<ILogger<CreateIbsSssAssessmentCommandHandler>>();

    private CreateIbsSssAssessmentCommandHandler CreateHandler() => new(
        _currentUserService, _userRepository, _assessmentRepository, _scheduleRepository, _outboxWriter, _unitOfWork, _mediator, _logger);

    private void ArrangePatient()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc", "p@cauce.local", "Paciente", PatientRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
    }

    private static CreateIbsSssAssessmentCommand Command(AssessmentType type) => new(type, 50, 50, 50, 50, 50);

    [Fact]
    public async Task Handle_Baseline_TriggersOnboarding()
    {
        ArrangePatient();
        _assessmentRepository.FindBaselineByPatientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((IbsSssAssessment?)null);

        var result = await CreateHandler().Handle(Command(AssessmentType.Baseline), CancellationToken.None);

        result.TriggeredOnboardingCompletion.Should().BeTrue();
        result.TotalScore.Should().Be(250);
        await _mediator.Received(1).Send(Arg.Any<CompletePatientOnboardingCommand>(), Arg.Any<CancellationToken>());
        // La auditoría de ibs_sss_assessments la realiza el trigger de PostgreSQL (DEC-B5-03),
        // no el handler; por eso ya no se verifica una llamada explícita a IAuditLogger.
    }

    [Fact]
    public async Task Handle_SecondBaseline_Throws()
    {
        ArrangePatient();
        var existing = IbsSssAssessment.Submit(Guid.NewGuid(), Guid.NewGuid(), AssessmentType.Baseline, 0, 10, 10, 10, 10, 10, DateTime.UtcNow);
        _assessmentRepository.FindBaselineByPatientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(existing);

        var act = () => CreateHandler().Handle(Command(AssessmentType.Baseline), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateBaselineAssessmentException>();
    }

    [Fact]
    public async Task Handle_Periodic_AutogeneratesCycleAndDoesNotTriggerOnboarding()
    {
        ArrangePatient();
        _assessmentRepository.GetNextCycleNumberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(3);

        var result = await CreateHandler().Handle(Command(AssessmentType.Periodic), CancellationToken.None);

        result.TriggeredOnboardingCompletion.Should().BeFalse();
        await _mediator.DidNotReceive().Send(Arg.Any<CompletePatientOnboardingCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Baseline_SchedulesNextAssessment()
    {
        ArrangePatient();
        _assessmentRepository.FindBaselineByPatientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((IbsSssAssessment?)null);
        _scheduleRepository.FindOpenByPatientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((IbsSssAssessmentSchedule?)null);

        await CreateHandler().Handle(Command(AssessmentType.Baseline), CancellationToken.None);

        await _scheduleRepository.Received(1).AddAsync(
            Arg.Is<IbsSssAssessmentSchedule>(schedule => !schedule.Completed && !schedule.Missed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Periodic_ClosesOpenScheduleAndCreatesNext()
    {
        ArrangePatient();
        _assessmentRepository.GetNextCycleNumberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(2);
        var openSchedule = IbsSssAssessmentSchedule.Create(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-15));
        _scheduleRepository.FindOpenByPatientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(openSchedule);

        await CreateHandler().Handle(Command(AssessmentType.Periodic), CancellationToken.None);

        openSchedule.Completed.Should().BeTrue();
        await _scheduleRepository.Received(1).AddAsync(Arg.Any<IbsSssAssessmentSchedule>(), Arg.Any<CancellationToken>());
    }
}
