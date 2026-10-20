using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Services;
using Cauce.Domain.Recommendations.ValueObjects;
using FluentAssertions;

namespace Cauce.Domain.Tests.Recommendations;

/// <summary>
/// Pruebas de <see cref="RecommendationActionPolicy"/>: qué acciones ofrece el detalle según el estado,
/// la vigencia, el archivado y quién consulta.
/// </summary>
public sealed class RecommendationActionPolicyTests
{
    private static readonly DateTime Now = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromHours(72);

    public static TheoryData<string, RecommendationAction[]> ActionsByState => new()
    {
        { "Generated", [] },
        { "PendingReview", [RecommendationAction.Approve, RecommendationAction.Modify, RecommendationAction.Reject] },
        { "Approved", [RecommendationAction.Archive] },
        { "ModifiedApproved", [RecommendationAction.Archive] },
        { "ManualApproved", [RecommendationAction.Archive] },
        { "Rejected", [] },
        { "Delivered", [] },
        { "FeedbackReceived", [] },
        { "Expired", [] },
    };

    [Theory]
    [MemberData(nameof(ActionsByState))]
    public void GetAllowedActions_AssignedNutritionist_DependsOnState(string state, RecommendationAction[] expected)
    {
        var recommendation = InState(state);

        var actions = RecommendationActionPolicy.GetAllowedActions(recommendation, Now, callerIsAssignedNutritionist: true);

        actions.Should().Equal(expected);
    }

    [Theory]
    [MemberData(nameof(ActionsByState))]
    public void GetAllowedActions_NotAssignedNutritionist_IsEmptyInAnyState(string state, RecommendationAction[] expected)
    {
        _ = expected;
        var recommendation = InState(state);

        var actions = RecommendationActionPolicy.GetAllowedActions(recommendation, Now, callerIsAssignedNutritionist: false);

        actions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void GetAllowedActions_PendingReviewPastItsWindow_IsEmpty()
    {
        var recommendation = InState("PendingReview");

        var actions = RecommendationActionPolicy.GetAllowedActions(
            recommendation, Now + Window + TimeSpan.FromMinutes(1), callerIsAssignedNutritionist: true);

        actions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("ModifiedApproved")]
    [InlineData("ManualApproved")]
    public void GetAllowedActions_ArchivedApprovedState_IsEmpty(string state)
    {
        var recommendation = InState(state);
        recommendation.Archive(ArchiveReason.ObjectiveMet, Now);

        var actions = RecommendationActionPolicy.GetAllowedActions(recommendation, Now, callerIsAssignedNutritionist: true);

        actions.Should().BeEmpty();
    }

    private static Recommendation InState(string state)
    {
        switch (state)
        {
            case "Generated":
                return Generate();
            case "PendingReview":
                return PendingReview();
            case "Approved":
            {
                var recommendation = PendingReview();
                recommendation.Approve(Guid.NewGuid(), "nota clínica válida", Now);
                return recommendation;
            }
            case "ModifiedApproved":
            {
                var recommendation = PendingReview();
                recommendation.ModifyByNutritionist(Guid.NewGuid(), "nota de modificación", Now);
                return recommendation;
            }
            case "ManualApproved":
                return Recommendation.CreateManual(
                    Guid.NewGuid(), Guid.NewGuid(), "Título", "Descripción", null, "nota clínica suficiente", null, Now);
            case "Rejected":
            {
                var recommendation = PendingReview();
                recommendation.Reject(Guid.NewGuid(), "motivo de rechazo", Now);
                return recommendation;
            }
            case "Delivered":
                return Delivered();
            case "FeedbackReceived":
            {
                var recommendation = Delivered();
                var feedback = RecommendationFeedback.Submit(
                    recommendation.Id, true, FeedbackOutcome.Improvement, null, SyncStatus.SyncCompleted, Now);
                recommendation.RecordFeedback(feedback, Now);
                return recommendation;
            }
            case "Expired":
            {
                var recommendation = PendingReview();
                recommendation.Expire(Now);
                return recommendation;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private static Recommendation Generate()
    {
        var items = new[] { RecommendationItem.Create(Guid.NewGuid(), ActionType.Avoid, "razón", null) };

        return Recommendation.Generate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ConfidenceScore.Create(0.5m),
            ExplanationSource.LlmGenerated,
            "explicación",
            items,
            Now,
            Window);
    }

    private static Recommendation PendingReview()
    {
        var recommendation = Generate();
        recommendation.MarkPendingReview(Now);
        return recommendation;
    }

    private static Recommendation Delivered()
    {
        var recommendation = PendingReview();
        recommendation.Approve(Guid.NewGuid(), "nota clínica válida", Now);
        recommendation.Deliver(Now);
        return recommendation;
    }
}
