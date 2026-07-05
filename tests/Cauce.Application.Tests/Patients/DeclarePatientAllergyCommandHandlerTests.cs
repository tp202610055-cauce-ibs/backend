using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.UseCases.DeclarePatientAllergy;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Patients.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Patients;

/// <summary>
/// Pruebas del handler <see cref="DeclarePatientAllergyCommandHandler"/>.
/// </summary>
public sealed class DeclarePatientAllergyCommandHandlerTests
{
    private const int PatientRoleId = 1;

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAllergyRepository _allergyRepository = Substitute.For<IAllergyRepository>();
    private readonly IPatientAllergyRepository _patientAllergyRepository = Substitute.For<IPatientAllergyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<DeclarePatientAllergyCommandHandler> _logger = Substitute.For<ILogger<DeclarePatientAllergyCommandHandler>>();

    private DeclarePatientAllergyCommandHandler CreateHandler() => new(
        _currentUserService,
        _userRepository,
        _allergyRepository,
        _patientAllergyRepository,
        _unitOfWork,
        _logger);

    private void ArrangePatient()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-patient", "p@cauce.local", "Paciente", PatientRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
    }

    [Fact]
    public async Task Handle_ValidActiveAllergy_DeclaresAllergy()
    {
        ArrangePatient();
        var allergyId = Guid.NewGuid();
        _allergyRepository.FindByIdAsync(allergyId, Arg.Any<CancellationToken>())
            .Returns(Allergy.SeedEntry(allergyId, "Gluten", AllergyType.Intolerance, "desc"));
        _patientAllergyRepository.ExistsAsync(Arg.Any<Guid>(), allergyId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(
            new DeclarePatientAllergyCommand(allergyId, AllergySeverity.Moderate, null), CancellationToken.None);

        result.PatientAllergyId.Should().NotBeEmpty();
        await _patientAllergyRepository.Received(1).AddAsync(Arg.Any<PatientAllergy>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InactiveAllergy_ThrowsAllergyNotFound()
    {
        ArrangePatient();
        var allergyId = Guid.NewGuid();
        var inactive = Allergy.SeedEntry(allergyId, "Gluten", AllergyType.Intolerance, "desc");
        inactive.Deactivate();
        _allergyRepository.FindByIdAsync(allergyId, Arg.Any<CancellationToken>()).Returns(inactive);

        var act = () => CreateHandler().Handle(
            new DeclarePatientAllergyCommand(allergyId, AllergySeverity.Mild, null), CancellationToken.None);

        await act.Should().ThrowAsync<AllergyNotFoundException>();
    }

    [Fact]
    public async Task Handle_DuplicateDeclaration_ThrowsDuplicate()
    {
        ArrangePatient();
        var allergyId = Guid.NewGuid();
        _allergyRepository.FindByIdAsync(allergyId, Arg.Any<CancellationToken>())
            .Returns(Allergy.SeedEntry(allergyId, "Gluten", AllergyType.Intolerance, "desc"));
        _patientAllergyRepository.ExistsAsync(Arg.Any<Guid>(), allergyId, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateHandler().Handle(
            new DeclarePatientAllergyCommand(allergyId, AllergySeverity.Mild, null), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicatePatientAllergyException>();
    }
}
