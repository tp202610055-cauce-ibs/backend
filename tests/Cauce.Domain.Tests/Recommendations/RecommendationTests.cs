using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;
using Cauce.Domain.Recommendations.ValueObjects;
using FluentAssertions;

namespace Cauce.Domain.Tests.Recommendations;

/// <summary>
/// Pruebas de las invariantes y transiciones de estado de <see cref="Recommendation"/>.
/// </summary>
public sealed class RecommendationTests
{
    private static readonly DateTime Now = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromHours(72);

    private static Recommendation Generate(int itemCount = 1)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(_ => RecommendationItem.Create(Guid.NewGuid(), ActionType.Avoid, "razón", null))
            .ToList();

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

    private static Recommendation Approved()
    {
        var recommendation = Generate();
        recommendation.MarkPendingReview(Now);
        recommendation.Approve(Guid.NewGuid(), "nota clínica válida", Now);
        return recommendation;
    }

    private static Recommendation Delivered()
    {
        var recommendation = Approved();
        recommendation.Deliver(Now);
        return recommendation;
    }

    [Fact]
    public void Generate_WithValidInputs_CreatesInGeneratedState()
    {
        var recommendation = Generate();

        recommendation.Status.Should().Be(RecommendationStatus.Generated);
        recommendation.GeneratedAt.Should().Be(Now);
        recommendation.ExpiresAt.Should().Be(Now + Window);
        recommendation.Items.Should().HaveCount(1);
    }

    [Fact]
    public void Generate_WithEmptyItems_ThrowsEmptyRecommendationException()
    {
        var act = () => Recommendation.Generate(
            Guid.NewGuid(), Guid.NewGuid(), ConfidenceScore.Create(0.5m),
            ExplanationSource.Fallback, null, Array.Empty<RecommendationItem>(), Now, Window);

        act.Should().Throw<EmptyRecommendationException>();
    }

    [Fact]
    public void MarkPendingReview_FromGenerated_TransitsCorrectly()
    {
        var recommendation = Generate();

        recommendation.MarkPendingReview(Now);

        recommendation.Status.Should().Be(RecommendationStatus.PendingReview);
    }

    [Fact]
    public void MarkPendingReview_FromApproved_Throws()
    {
        var recommendation = Approved();

        var act = () => recommendation.MarkPendingReview(Now);

        act.Should().Throw<InvalidRecommendationStateTransitionException>();
    }

    [Fact]
    public void Approve_FromPendingReview_TransitsAndCapturesData()
    {
        var recommendation = Generate();
        recommendation.MarkPendingReview(Now);
        var nutritionistId = Guid.NewGuid();

        recommendation.Approve(nutritionistId, "nota clínica válida", Now);

        recommendation.Status.Should().Be(RecommendationStatus.Approved);
        recommendation.ReviewedByNutritionistId.Should().Be(nutritionistId);
        recommendation.NutritionistNote.Should().Be("nota clínica válida");
        recommendation.AutoApproved.Should().BeFalse();
    }

    [Fact]
    public void Approve_WithEmptyNote_ThrowsArgumentException()
    {
        var recommendation = Generate();
        recommendation.MarkPendingReview(Now);

        var act = () => recommendation.Approve(Guid.NewGuid(), "   ", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AutoApprove_FromGenerated_SetsApprovedAndAutoApprovedTrue()
    {
        var recommendation = Generate();

        recommendation.AutoApprove(Now);

        recommendation.Status.Should().Be(RecommendationStatus.Approved);
        recommendation.AutoApproved.Should().BeTrue();
        recommendation.ReviewedByNutritionistId.Should().BeNull();
    }

    [Fact]
    public void Reject_FromPendingReview_Transits()
    {
        var recommendation = Generate();
        recommendation.MarkPendingReview(Now);

        recommendation.Reject(Guid.NewGuid(), "motivo del rechazo", Now);

        recommendation.Status.Should().Be(RecommendationStatus.Rejected);
        recommendation.NutritionistNote.Should().Be("motivo del rechazo");
    }

    [Fact]
    public void Deliver_FromApproved_SetsDeliveredAt()
    {
        var recommendation = Approved();

        recommendation.Deliver(Now);

        recommendation.Status.Should().Be(RecommendationStatus.Delivered);
        recommendation.DeliveredAt.Should().Be(Now);
    }

    [Fact]
    public void Deliver_FromPendingReview_Throws()
    {
        var recommendation = Generate();
        recommendation.MarkPendingReview(Now);

        var act = () => recommendation.Deliver(Now);

        act.Should().Throw<InvalidRecommendationStateTransitionException>();
    }

    [Fact]
    public void RecordFeedback_FromDelivered_TransitsToFeedbackReceived()
    {
        var recommendation = Delivered();
        var feedback = RecommendationFeedback.Submit(
            recommendation.Id, true, FeedbackOutcome.Improvement, null, SyncStatus.SyncCompleted, Now);

        recommendation.RecordFeedback(feedback, Now);

        recommendation.Status.Should().Be(RecommendationStatus.FeedbackReceived);
        recommendation.Feedback.Should().Be(feedback);
    }

    [Fact]
    public void RecordFeedback_FromApproved_Throws()
    {
        var recommendation = Approved();
        var feedback = RecommendationFeedback.Submit(
            recommendation.Id, true, FeedbackOutcome.Improvement, null, SyncStatus.SyncCompleted, Now);

        var act = () => recommendation.RecordFeedback(feedback, Now);

        act.Should().Throw<InvalidRecommendationStateTransitionException>();
    }

    [Fact]
    public void Expire_FromGenerated_TransitsCorrectly()
    {
        var recommendation = Generate();

        recommendation.Expire(Now);

        recommendation.Status.Should().Be(RecommendationStatus.Expired);
    }

    [Fact]
    public void Expire_FromDelivered_DoesNothing()
    {
        var recommendation = Delivered();

        recommendation.Expire(Now);

        recommendation.Status.Should().Be(RecommendationStatus.Delivered);
    }

    [Fact]
    public void IsExpired_WhenExpiresAtPast_ReturnsTrue()
    {
        var recommendation = Generate();

        recommendation.IsExpired(Now.AddHours(73)).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenNotYetExpired_ReturnsFalse()
    {
        var recommendation = Generate();

        recommendation.IsExpired(Now.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenStatusDelivered_ReturnsFalse()
    {
        var recommendation = Delivered();

        recommendation.IsExpired(Now.AddHours(73)).Should().BeFalse();
    }
}
