using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using FluentAssertions;

namespace Cauce.Domain.Tests.Recommendations;

/// <summary>
/// Pruebas de la entidad <see cref="RecommendationFeedback"/> y su etiqueta de entrenamiento.
/// </summary>
public sealed class RecommendationFeedbackTests
{
    private static readonly DateTime Now = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

    private static RecommendationFeedback Submit(bool wasApplied, FeedbackOutcome outcome) =>
        RecommendationFeedback.Submit(Guid.NewGuid(), wasApplied, outcome, null, SyncStatus.SyncCompleted, Now);

    [Fact]
    public void Submit_SetsFields()
    {
        var feedback = Submit(true, FeedbackOutcome.Improvement);

        feedback.WasApplied.Should().BeTrue();
        feedback.Outcome.Should().Be(FeedbackOutcome.Improvement);
        feedback.SubmittedAt.Should().Be(Now);
    }

    [Fact]
    public void ToTrainingLabel_WhenNotApplied_Throws()
    {
        var feedback = Submit(false, FeedbackOutcome.Worsening);

        var act = () => feedback.ToTrainingLabel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToTrainingLabel_WhenAppliedAndWorsening_ReturnsOne()
    {
        Submit(true, FeedbackOutcome.Worsening).ToTrainingLabel().Should().Be(1);
    }

    [Theory]
    [InlineData(FeedbackOutcome.Improvement)]
    [InlineData(FeedbackOutcome.NoChange)]
    public void ToTrainingLabel_WhenAppliedAndNotWorsening_ReturnsZero(FeedbackOutcome outcome)
    {
        Submit(true, outcome).ToTrainingLabel().Should().Be(0);
    }
}
