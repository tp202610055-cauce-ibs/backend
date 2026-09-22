using Cauce.Domain.ClinicalRegistry;
using FluentAssertions;

namespace Cauce.Domain.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas de la agenda de evaluaciones IBS-SSS, en particular de la tolerancia con la que acepta un
/// envío antes de la fecha de vencimiento.
/// </summary>
public sealed class IbsSssAssessmentScheduleTests
{
    private static readonly DateTime DueDate = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IbsSssAssessmentSchedule CreateSchedule() =>
        IbsSssAssessmentSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), DueDate, DueDate.AddDays(-14));

    [Fact]
    public void AcceptedFrom_IsDueDateMinusTolerance()
    {
        CreateSchedule().AcceptedFrom.Should().Be(DueDate - IbsSssAssessmentSchedule.EarlySubmissionTolerance);
    }

    [Fact]
    public void IsTooEarlyFor_BeforeTolerance_IsTrue()
    {
        var schedule = CreateSchedule();

        schedule.IsTooEarlyFor(DueDate.AddDays(-3)).Should().BeTrue();
    }

    [Fact]
    public void IsTooEarlyFor_WithinTolerance_IsFalse()
    {
        var schedule = CreateSchedule();

        // Justo en el límite y dentro de él: la tolerancia se aplica de forma inclusiva.
        schedule.IsTooEarlyFor(schedule.AcceptedFrom).Should().BeFalse();
        schedule.IsTooEarlyFor(DueDate.AddHours(-2)).Should().BeFalse();
    }

    [Fact]
    public void IsTooEarlyFor_OneTickBeforeTolerance_IsTrue()
    {
        var schedule = CreateSchedule();

        schedule.IsTooEarlyFor(schedule.AcceptedFrom.AddTicks(-1)).Should().BeTrue();
    }

    [Fact]
    public void IsTooEarlyFor_AfterDueDate_IsFalse()
    {
        CreateSchedule().IsTooEarlyFor(DueDate.AddDays(5)).Should().BeFalse();
    }

    [Fact]
    public void IsTooEarlyFor_CompletedSchedule_IsFalse()
    {
        var schedule = CreateSchedule();
        schedule.MarkCompleted(DueDate.AddDays(-10));

        // Una agenda cerrada ya no gobierna el ciclo: el siguiente envío lo gobierna la nueva agenda.
        schedule.IsTooEarlyFor(DueDate.AddDays(-10)).Should().BeFalse();
    }

    [Fact]
    public void IsTooEarlyFor_MissedSchedule_IsFalse()
    {
        var schedule = CreateSchedule();
        schedule.MarkMissed();

        schedule.IsTooEarlyFor(DueDate.AddDays(-10)).Should().BeFalse();
    }
}
