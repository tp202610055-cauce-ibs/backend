using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Application.Patients.UseCases.ListAssignedPatients;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Enums;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Patients;

/// <summary>
/// Pruebas del handler <see cref="ListAssignedPatientsQueryHandler"/>.
/// </summary>
public sealed class ListAssignedPatientsQueryHandlerTests
{
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly INutritionistPatientRepository _nutritionistPatientRepository = Substitute.For<INutritionistPatientRepository>();

    private ListAssignedPatientsQueryHandler CreateHandler() => new(
        _currentUserService,
        _userRepository,
        _nutritionistPatientRepository);

    [Fact]
    public async Task Handle_AsNutritionist_ReturnsAssignedSummaries()
    {
        var nutritionist = User.CreateNutritionist(Guid.NewGuid(), "kc-nutri", "n@cauce.local", "Nutri", NutritionistRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(nutritionist);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);
        var summaries = new List<AssignedPatientSummary>
        {
            new(Guid.NewGuid(), "Paciente A", Guid.NewGuid(), DateTime.UtcNow, true, IbsSubtype.IbsD)
        };
        _nutritionistPatientRepository.ListAssignedPatientSummariesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(summaries);

        var result = await CreateHandler().Handle(new ListAssignedPatientsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].PatientFullName.Should().Be("Paciente A");
    }

    [Fact]
    public async Task Handle_AsPatient_ThrowsUnauthorized()
    {
        var patient = User.CreatePatient(Guid.NewGuid(), "kc-patient", "p@cauce.local", "Paciente", PatientRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(patient);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);

        var act = () => CreateHandler().Handle(new ListAssignedPatientsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
