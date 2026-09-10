using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.UseCases.CreatePatientProfile;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Patients.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Patients;

/// <summary>
/// Pruebas del handler <see cref="CreatePatientProfileCommandHandler"/>.
/// </summary>
public sealed class CreatePatientProfileCommandHandlerTests
{
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPatientProfileRepository _patientProfileRepository = Substitute.For<IPatientProfileRepository>();
    private readonly IPatientNutritionistAssignmentService _assignmentService =
        Substitute.For<IPatientNutritionistAssignmentService>();
    private readonly IBmiCalculator _bmiCalculator = Substitute.For<IBmiCalculator>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<CreatePatientProfileCommandHandler> _logger = Substitute.For<ILogger<CreatePatientProfileCommandHandler>>();

    private CreatePatientProfileCommandHandler CreateHandler() => new(
        _currentUserService,
        _userRepository,
        _patientProfileRepository,
        _assignmentService,
        _bmiCalculator,
        _unitOfWork,
        _logger);

    private static CreatePatientProfileCommand ValidCommand() => new(
        DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30),
        BiologicalSex.Male,
        70m,
        175m,
        IbsSubtype.IbsM,
        null,
        null);

    private User ArrangePatient()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-patient", "p@cauce.local", "Paciente", PatientRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
        _bmiCalculator.Calculate(Arg.Any<decimal>(), Arg.Any<decimal>()).Returns(22.86m);
        _bmiCalculator.Categorize(Arg.Any<decimal>()).Returns("normal");
        return user;
    }

    [Fact]
    public async Task Handle_ValidRequestWithoutInvitation_CreatesProfileWithoutAssignment()
    {
        ArrangePatient();
        _patientProfileRepository.ExistsByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _assignmentService
            .EstablishAssignmentAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((false, (Guid?)null));

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        result.NutritionistAssigned.Should().BeFalse();
        result.NutritionistAssignmentId.Should().BeNull();
        await _patientProfileRepository.Received(1).AddAsync(Arg.Any<PatientProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PatientUsedInvitation_CreatesNutritionistAssignment()
    {
        ArrangePatient();
        _patientProfileRepository.ExistsByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        var assignmentId = Guid.NewGuid();
        _assignmentService
            .EstablishAssignmentAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((true, (Guid?)assignmentId));

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        result.NutritionistAssigned.Should().BeTrue();
        result.NutritionistAssignmentId.Should().Be(assignmentId);
        await _assignmentService.Received(1)
            .EstablishAssignmentAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProfileAlreadyExists_ThrowsDuplicate()
    {
        ArrangePatient();
        _patientProfileRepository.ExistsByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicatePatientProfileException>();
    }

    [Fact]
    public async Task Handle_NonPatientRole_ThrowsUnauthorized()
    {
        var nutritionist = User.CreateNutritionist(Guid.NewGuid(), "kc-nutri", "n@cauce.local", "Nutri", NutritionistRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(nutritionist);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);

        var act = () => CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
