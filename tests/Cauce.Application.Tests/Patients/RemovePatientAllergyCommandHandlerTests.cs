using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.UseCases.RemovePatientAllergy;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Patients;

/// <summary>
/// Pruebas del handler <see cref="RemovePatientAllergyCommandHandler"/>.
/// </summary>
public sealed class RemovePatientAllergyCommandHandlerTests
{
    private const int PatientRoleId = 1;

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPatientAllergyRepository _patientAllergyRepository = Substitute.For<IPatientAllergyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<RemovePatientAllergyCommandHandler> _logger = Substitute.For<ILogger<RemovePatientAllergyCommandHandler>>();

    private RemovePatientAllergyCommandHandler CreateHandler() => new(
        _currentUserService,
        _userRepository,
        _patientAllergyRepository,
        _unitOfWork,
        _logger);

    private User ArrangePatient()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-patient", "p@cauce.local", "Paciente", PatientRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
        return user;
    }

    [Fact]
    public async Task Handle_OwnDeclaration_RemovesIt()
    {
        var user = ArrangePatient();
        var declaration = PatientAllergy.Declare(Guid.NewGuid(), user.Id, Guid.NewGuid(), AllergySeverity.Mild, null, DateTime.UtcNow);
        _patientAllergyRepository.FindByIdAsync(declaration.Id, Arg.Any<CancellationToken>()).Returns(declaration);

        await CreateHandler().Handle(new RemovePatientAllergyCommand(declaration.Id), CancellationToken.None);

        _patientAllergyRepository.Received(1).Remove(declaration);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OtherPatientsDeclaration_ThrowsUnauthorized()
    {
        ArrangePatient();
        var otherPatientId = Guid.NewGuid();
        var declaration = PatientAllergy.Declare(Guid.NewGuid(), otherPatientId, Guid.NewGuid(), AllergySeverity.Mild, null, DateTime.UtcNow);
        _patientAllergyRepository.FindByIdAsync(declaration.Id, Arg.Any<CancellationToken>()).Returns(declaration);

        var act = () => CreateHandler().Handle(new RemovePatientAllergyCommand(declaration.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _patientAllergyRepository.DidNotReceive().Remove(Arg.Any<PatientAllergy>());
    }
}
