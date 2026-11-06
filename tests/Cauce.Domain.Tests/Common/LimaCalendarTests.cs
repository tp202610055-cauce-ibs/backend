using System.Globalization;
using Cauce.Domain.Common;
using FluentAssertions;

namespace Cauce.Domain.Tests.Common;

/// <summary>
/// Pruebas de frontera de <see cref="LimaCalendar"/>: entre las 19:00 y las 24:00 de Lima el día UTC ya es el siguiente.
/// </summary>
public sealed class LimaCalendarTests
{
    [Theory]
    [InlineData("2026-11-05T23:59:59Z", "2026-11-05")]
    [InlineData("2026-11-06T00:00:00Z", "2026-11-05")]
    [InlineData("2026-11-06T04:59:59Z", "2026-11-05")]
    [InlineData("2026-11-06T05:00:00Z", "2026-11-06")]
    public void DateOf_InstantAroundUtcMidnight_ReturnsTheLimaDay(string instant, string expectedDay)
    {
        LimaCalendar.DateOf(Utc(instant)).Should().Be(Day(expectedDay));
    }

    [Theory]
    [InlineData("2026-11-05", "2026-11-05T05:00:00Z", "2026-11-06T05:00:00Z")]
    [InlineData("2026-10-31", "2026-10-31T05:00:00Z", "2026-11-01T05:00:00Z")]
    [InlineData("2026-12-31", "2026-12-31T05:00:00Z", "2027-01-01T05:00:00Z")]
    public void StartAndEndOfDayUtc_LimaDay_SpanFrom0500UtcToTheNext0500Utc(string day, string expectedStart, string expectedEnd)
    {
        var limaDay = Day(day);

        var start = LimaCalendar.StartOfDayUtc(limaDay);
        var end = LimaCalendar.EndOfDayUtc(limaDay);

        start.Should().Be(Utc(expectedStart));
        start.Kind.Should().Be(DateTimeKind.Utc);
        end.Should().Be(Utc(expectedEnd));
        end.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("2026-11-05")]
    [InlineData("2026-10-31")]
    [InlineData("2026-12-31")]
    public void DateOf_BoundsOfALimaDay_StartIsInsideAndEndIsTheNextDay(string day)
    {
        var limaDay = Day(day);

        LimaCalendar.DateOf(LimaCalendar.StartOfDayUtc(limaDay)).Should().Be(limaDay);
        LimaCalendar.DateOf(LimaCalendar.EndOfDayUtc(limaDay).AddTicks(-1)).Should().Be(limaDay);
        LimaCalendar.DateOf(LimaCalendar.EndOfDayUtc(limaDay)).Should().Be(limaDay.AddDays(1));
    }

    private static DateTime Utc(string instant) =>
        DateTime.Parse(instant, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    private static DateOnly Day(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
