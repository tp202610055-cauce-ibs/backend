using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Application.Patients.UseCases.ListAssignedPatients;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Enums;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Patients;

/// <summary>
/// Pruebas del handler <see cref="ListAssignedPatientsQueryHandler"/> (panel de triaje US18).
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

    private void ArrangeNutritionist()
    {
        var nutritionist = User.CreateNutritionist(Guid.NewGuid(), "kc-nutri", "n@cauce.local", "Nutri", NutritionistRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(nutritionist);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);
    }

    private static AssignedPatientTriageRow Row(
        string name,
        int? latestScore = null,
        SeverityCategory? severity = null,
        DateTime? lastActivity = null,
        int pendingOver24h = 0,
        DateTime? assignedAt = null)
    {
        return new AssignedPatientTriageRow(
            Guid.NewGuid(),
            name,
            Guid.NewGuid(),
            assignedAt ?? DateTime.UtcNow,
            ProfileCompleted: true,
            IbsSubtype.IbsD,
            latestScore,
            severity,
            lastActivity,
            LastSymptomAt: null,
            LastAssessmentAt: null,
            pendingOver24h);
    }

    [Fact]
    public async Task Handle_AsNutritionist_MapsRowsAndComputesLastActivity()
    {
        ArrangeNutritionist();
        var activity = DateTime.UtcNow.AddDays(-1);
        var rows = new List<AssignedPatientTriageRow>
        {
            Row("Paciente A", latestScore: 120, severity: SeverityCategory.Mild, lastActivity: activity)
        };
        _nutritionistPatientRepository
            .ListAssignedPatientTriageRowsAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(rows);

        var result = await CreateHandler().Handle(new ListAssignedPatientsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].PatientFullName.Should().Be("Paciente A");
        result[0].LatestIbsSssScore.Should().Be(120);
        result[0].LastActivityAt.Should().Be(activity);
        result[0].PriorityLevel.Should().Be(PriorityLevel.Low);
    }

    [Fact]
    public async Task Handle_MixedPatients_OrdersByPriorityThenScore()
    {
        ArrangeNutritionist();
        var rows = new List<AssignedPatientTriageRow>
        {
            Row("Baja severidad", latestScore: 100, severity: SeverityCategory.Mild, lastActivity: DateTime.UtcNow.AddHours(-2)),
            Row("Sin datos"),
            Row("Pendiente vencida", latestScore: 150, severity: SeverityCategory.Mild, pendingOver24h: 2),
            Row("Severo", latestScore: 400, severity: SeverityCategory.Severe),
            Row("Moderado", latestScore: 250, severity: SeverityCategory.Moderate)
        };
        _nutritionistPatientRepository
            .ListAssignedPatientTriageRowsAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(rows);

        var result = await CreateHandler().Handle(new ListAssignedPatientsQuery(), CancellationToken.None);

        result.Select(r => r.PriorityLevel).Should().ContainInOrder(
            PriorityLevel.High, PriorityLevel.High, PriorityLevel.Medium, PriorityLevel.Low, PriorityLevel.None);
        // Entre las dos High, primero la de mayor puntaje (Severo 400 sobre Pendiente 150).
        result[0].PatientFullName.Should().Be("Severo");
        result[1].PatientFullName.Should().Be("Pendiente vencida");
        result.Last().PatientFullName.Should().Be("Sin datos");
    }

    [Fact]
    public async Task Handle_ProlongedInactivity_MarksMediumPriority()
    {
        ArrangeNutritionist();
        var rows = new List<AssignedPatientTriageRow>
        {
            Row("Inactivo", latestScore: 100, severity: SeverityCategory.Mild, lastActivity: DateTime.UtcNow.AddDays(-10))
        };
        _nutritionistPatientRepository
            .ListAssignedPatientTriageRowsAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(rows);

        var result = await CreateHandler().Handle(new ListAssignedPatientsQuery(), CancellationToken.None);

        result[0].PriorityLevel.Should().Be(PriorityLevel.Medium);
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
