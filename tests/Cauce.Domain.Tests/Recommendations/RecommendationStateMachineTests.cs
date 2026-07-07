using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;
using Cauce.Domain.Recommendations.Services;
using FluentAssertions;

namespace Cauce.Domain.Tests.Recommendations;

/// <summary>
/// Pruebas de la máquina de estados de la recomendación, cubriendo la matriz completa de
/// transiciones (7×7).
/// </summary>
public sealed class RecommendationStateMachineTests
{
    private static readonly IReadOnlyDictionary<RecommendationStatus, RecommendationStatus[]> Valid =
        new Dictionary<RecommendationStatus, RecommendationStatus[]>
        {
            [RecommendationStatus.Generated] = new[] { RecommendationStatus.PendingReview, RecommendationStatus.Approved, RecommendationStatus.Expired },
            [RecommendationStatus.PendingReview] = new[] { RecommendationStatus.Approved, RecommendationStatus.ModifiedApproved, RecommendationStatus.Rejected, RecommendationStatus.Expired },
            [RecommendationStatus.Approved] = new[] { RecommendationStatus.Delivered, RecommendationStatus.Expired },
            [RecommendationStatus.ModifiedApproved] = new[] { RecommendationStatus.Delivered, RecommendationStatus.Expired },
            [RecommendationStatus.ManualApproved] = new[] { RecommendationStatus.Delivered, RecommendationStatus.Expired },
            [RecommendationStatus.Delivered] = new[] { RecommendationStatus.FeedbackReceived },
            [RecommendationStatus.Rejected] = Array.Empty<RecommendationStatus>(),
            [RecommendationStatus.FeedbackReceived] = Array.Empty<RecommendationStatus>(),
            [RecommendationStatus.Expired] = Array.Empty<RecommendationStatus>()
        };

    public static IEnumerable<object[]> AllTransitions()
    {
        foreach (var from in Enum.GetValues<RecommendationStatus>())
        {
            foreach (var to in Enum.GetValues<RecommendationStatus>())
            {
                yield return new object[] { from, to, Valid[from].Contains(to) };
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllTransitions))]
    public void CanTransition_MatchesMatrix(RecommendationStatus from, RecommendationStatus to, bool expected)
    {
        RecommendationStateMachine.CanTransition(from, to).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(AllTransitions))]
    public void EnsureTransition_ThrowsOnlyForInvalid(RecommendationStatus from, RecommendationStatus to, bool expected)
    {
        var act = () => RecommendationStateMachine.EnsureTransition(from, to);

        if (expected)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<InvalidRecommendationStateTransitionException>();
        }
    }
}
