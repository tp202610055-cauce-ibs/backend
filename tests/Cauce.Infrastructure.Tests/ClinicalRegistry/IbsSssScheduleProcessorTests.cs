using Cauce.Infrastructure.ClinicalRegistry;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas del corte de día en Lima (UTC-5) con el que <see cref="IbsSssScheduleProcessor"/> decide qué
/// agendas vencen hoy. El resto del procesador se prueba en integración.
/// </summary>
public sealed class IbsSssScheduleProcessorTests
{
    [Theory]
    [InlineData("2026-10-24T14:00:00Z", "2026-10-25T05:00:00Z")]
    [InlineData("2026-10-24T03:00:00Z", "2026-10-24T05:00:00Z")]
    [InlineData("2026-10-24T05:00:00Z", "2026-10-25T05:00:00Z")]
    [InlineData("2026-10-24T04:59:59Z", "2026-10-24T05:00:00Z")]
    public void EndOfLimaDayUtc_ReturnsNextLimaMidnightInUtc(string utcNow, string expected)
    {
        var now = DateTime.Parse(utcNow, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        var end = IbsSssScheduleProcessor.EndOfLimaDayUtc(now);

        end.Should().Be(DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal));
        end.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void EndOfLimaDayUtc_ScheduleDueAtUtcMidnight_FallsOnThePreviousLimaDay()
    {
        // La agenda vence a medianoche UTC, que en Lima son las 19:00 del día anterior.
        var dueDate = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
        var nineInLimaTheDayBefore = new DateTime(2026, 10, 24, 14, 0, 0, DateTimeKind.Utc);

        (dueDate < IbsSssScheduleProcessor.EndOfLimaDayUtc(nineInLimaTheDayBefore)).Should().BeTrue();
    }
}
