using Cauce.Infrastructure.Qa.Manifest;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Qa;

public sealed class QaClockTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 10, 24, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Resolve_HoursAndDays_SubtractFromTheAnchor()
    {
        var clock = new QaClock(Anchor);

        clock.Resolve(QaOffset.HoursAgo(30)).Should().Be(Anchor.UtcDateTime.AddHours(-30));
        clock.Resolve(QaOffset.DaysAgo(14)).Should().Be(Anchor.UtcDateTime.AddDays(-14));
        clock.Resolve(new QaOffset(null, null, true, null)).Should().Be(Anchor.UtcDateTime);
    }

    [Fact]
    public void Resolve_FixedDate_IsNoonInLima()
    {
        var clock = new QaClock(Anchor);

        clock.Resolve(new QaOffset(null, null, false, new DateOnly(2026, 8, 5)))
            .Should().Be(new DateTime(2026, 8, 5, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void LimaToday_JustAfterUtcMidnight_IsStillThePreviousDay()
    {
        var clock = new QaClock(new DateTimeOffset(2026, 10, 25, 3, 0, 0, TimeSpan.Zero));

        clock.LimaToday.Should().Be(new DateOnly(2026, 10, 24));
        clock.LimaTodayStartUtc.Should().Be(new DateTime(2026, 10, 24, 5, 0, 0, DateTimeKind.Utc));
    }
}
