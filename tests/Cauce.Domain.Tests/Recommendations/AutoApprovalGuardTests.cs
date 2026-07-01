using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Services;
using Cauce.Domain.Recommendations.ValueObjects;
using FluentAssertions;

namespace Cauce.Domain.Tests.Recommendations;

/// <summary>
/// Pruebas del guard de auto-aprobación <see cref="AutoApprovalGuard"/> (DEC-B4-04 y DEC-B4-05).
/// </summary>
public sealed class AutoApprovalGuardTests
{
    private const decimal Threshold = 0.95m;
    private const int MaxAvoidItems = 3;

    private readonly AutoApprovalGuard _guard = new();

    private static IReadOnlyList<RecommendationItem> Items(int avoidCount)
    {
        var items = new List<RecommendationItem>();
        for (var i = 0; i < avoidCount; i++)
        {
            items.Add(RecommendationItem.Create(Guid.NewGuid(), ActionType.Avoid, null, null));
        }

        items.Add(RecommendationItem.Create(Guid.NewGuid(), ActionType.Suggest, null, null));
        return items;
    }

    [Fact]
    public void Evaluate_WhenDisabled_ReturnsFalse()
    {
        var decision = _guard.Evaluate(false, Threshold, MaxAvoidItems, ConfidenceScore.Create(0.99m), Items(0));

        decision.ShouldAutoApprove.Should().BeFalse();
        decision.Reasons.Should().Contain("AutoApprovalDisabled");
    }

    [Fact]
    public void Evaluate_WhenEnabledAndAboveThreshold_AndWithinAvoidLimit_ReturnsTrue()
    {
        var decision = _guard.Evaluate(true, Threshold, MaxAvoidItems, ConfidenceScore.Create(0.96m), Items(2));

        decision.ShouldAutoApprove.Should().BeTrue();
        decision.Reasons.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WhenBelowThreshold_ReturnsFalseWithReason()
    {
        var decision = _guard.Evaluate(true, Threshold, MaxAvoidItems, ConfidenceScore.Create(0.80m), Items(0));

        decision.ShouldAutoApprove.Should().BeFalse();
        decision.Reasons.Should().Contain("ConfidenceBelowThreshold");
    }

    [Fact]
    public void Evaluate_WhenTooManyAvoidItems_ReturnsFalseWithReason()
    {
        var decision = _guard.Evaluate(true, Threshold, MaxAvoidItems, ConfidenceScore.Create(0.99m), Items(4));

        decision.ShouldAutoApprove.Should().BeFalse();
        decision.Reasons.Should().Contain("TooManyAvoidItems");
    }
}
