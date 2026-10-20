using Cauce.Application.Patients.Dtos;
using Cauce.Application.Patients.Services;
using Cauce.Domain.Patients.Enums;
using FluentAssertions;

namespace Cauce.Application.Tests.Patients;

/// <summary>
/// Pruebas de <see cref="AssignedPatientTriage"/> con reloj fijo: los criterios de HU0018 y los pacientes
/// del caso CP049.
/// </summary>
public sealed class AssignedPatientTriageTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AssignedLongAgo = Now.AddDays(-30);

    private static AssignedPatientTriageRow Row(
        string name,
        int? score = null,
        DateTime? lastMealAt = null,
        DateTime? lastSymptomAt = null,
        DateTime? lastAssessmentAt = null,
        int pendingOver24h = 0,
        DateTime? assignedAt = null)
    {
        return new AssignedPatientTriageRow(
            Guid.NewGuid(),
            name,
            Guid.NewGuid(),
            assignedAt ?? AssignedLongAgo,
            ProfileCompleted: true,
            IbsSubtype.IbsD,
            score,
            LatestSeverity: null,
            lastMealAt,
            lastSymptomAt,
            lastAssessmentAt,
            pendingOver24h);
    }

    [Fact]
    public void Rank_PatientsFromTestCaseCP049_PutsHighFirstThenMediumThenTheRest()
    {
        var upToDate = Now.AddHours(-3);
        var rows = new[]
        {
            Row("P6", score: 150, lastMealAt: upToDate),
            // P4: su pendiente tiene 20 h, así que el repositorio no la cuenta entre las de más de 24 h.
            Row("P4", score: 200, lastMealAt: upToDate, pendingOver24h: 0),
            Row("P5", score: 200, lastMealAt: Now.AddDays(-4), lastSymptomAt: Now.AddDays(-4)),
            Row("P3", score: 300, lastMealAt: upToDate),
            Row("P2", score: 180, lastMealAt: upToDate, pendingOver24h: 1),
            Row("P1", score: 340, lastMealAt: upToDate, lastSymptomAt: upToDate),
        };

        var ranked = AssignedPatientTriage.Rank(rows, Now);

        ranked.Take(2).Should().OnlyContain(patient => patient.PriorityLevel == PriorityLevel.High);
        ranked.Take(2).Select(patient => patient.PatientFullName).Should().BeEquivalentTo("P1", "P2");
        ranked[2].PatientFullName.Should().Be("P5");
        ranked[2].PriorityLevel.Should().Be(PriorityLevel.Medium);
        ranked.Skip(3).Should().OnlyContain(patient => patient.PriorityLevel < PriorityLevel.Medium);
        ranked.Skip(3).Select(patient => patient.PatientFullName).Should().BeEquivalentTo("P3", "P4", "P6");
        // El desempate por puntaje IBS-SSS descendente fija el orden dentro de cada grupo.
        ranked.Select(patient => patient.PatientFullName).Should().Equal("P1", "P2", "P5", "P3", "P4", "P6");
    }

    [Theory]
    [InlineData(300, PriorityLevel.Low)]
    [InlineData(301, PriorityLevel.High)]
    public void ComputePriority_LatestScore_IsHighOnlyAbove300(int score, PriorityLevel expected)
    {
        AssignedPatientTriage.ComputePriority(Row("P", score: score, lastMealAt: Now.AddHours(-1)), Now).Should().Be(expected);
    }

    [Fact]
    public void ComputePriority_OnlyRecentActivityIsAnAssessment_IsMedium()
    {
        var row = Row("P", score: 150, lastMealAt: Now.AddDays(-5), lastAssessmentAt: Now.AddHours(-2));

        AssignedPatientTriage.ComputePriority(row, Now).Should().Be(PriorityLevel.Medium);
    }

    [Fact]
    public void ComputePriority_RecentSymptomWithOldMeal_IsNotMedium()
    {
        var row = Row("P", score: 150, lastMealAt: Now.AddDays(-5), lastSymptomAt: Now.AddHours(-2));

        AssignedPatientTriage.ComputePriority(row, Now).Should().Be(PriorityLevel.Low);
    }

    [Fact]
    public void ComputePriority_LastRecordExactly72HoursAgo_IsNotMedium()
    {
        var row = Row("P", score: 150, lastMealAt: Now.AddHours(-72));

        AssignedPatientTriage.ComputePriority(row, Now).Should().Be(PriorityLevel.Low);
    }

    [Fact]
    public void ComputePriority_NoRecordsAndAssignedFourDaysAgo_IsMedium()
    {
        var row = Row("P", assignedAt: Now.AddDays(-4));

        AssignedPatientTriage.ComputePriority(row, Now).Should().Be(PriorityLevel.Medium);
    }

    [Fact]
    public void ComputePriority_NoRecordsAndAssignedYesterday_IsNone()
    {
        var row = Row("P", assignedAt: Now.AddDays(-1));

        AssignedPatientTriage.ComputePriority(row, Now).Should().Be(PriorityLevel.None);
    }

    [Fact]
    public void ComputePriority_PendingReviewOver24Hours_IsHighEvenWhenInactive()
    {
        var row = Row("P", score: 100, lastMealAt: Now.AddDays(-10), pendingOver24h: 1);

        AssignedPatientTriage.ComputePriority(row, Now).Should().Be(PriorityLevel.High);
    }
}
