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
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ISender _mediator = Substitute.For<ISender>();
    private readonly ILogger<CreateIbsSssAssessmentCommandHandler> _logger = Substitute.For<ILogger<CreateIbsSssAssessmentCommandHandler>>();

    private CreateIbsSssAssessmentCommandHandler CreateHandler() => new(
        _currentUserService, _userRepository, _assessmentRepository, _unitOfWork, _auditLogger, _mediator, _logger);

    private void ArrangePatient()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc", "p@cauce.local", "Paciente", PatientRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
    }

    private static CreateIbsSssAssessmentCommand Command(AssessmentType type) => new(type, 50, 50, 50, 50, 50);

    [Fact]
    public async Task Handle_Baseline_TriggersOnboardingAndAudits()
    {
        ArrangePatient();
        _assessmentRepository.FindBaselineByPatientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((IbsSssAssessment?)null);

        var result = await CreateHandler().Handle(Command(AssessmentType.Baseline), CancellationToken.None);

        result.TriggeredOnboardingCompletion.Should().BeTrue();
        result.TotalScore.Should().Be(250);
        await _mediator.Received(1).Send(Arg.Any<CompletePatientOnboardingCommand>(), Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            Cauce.Domain.Auditing.Enums.AuditActionType.Create, nameof(IbsSssAssessment), Arg.Any<Guid?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
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
}
