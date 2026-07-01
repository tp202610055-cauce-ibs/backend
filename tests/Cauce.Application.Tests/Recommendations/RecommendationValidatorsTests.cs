using Cauce.Application.Recommendations.UseCases.ApproveRecommendation;
using Cauce.Application.Recommendations.UseCases.RejectRecommendation;
using Cauce.Application.Recommendations.UseCases.SubmitFeedback;
using Cauce.Domain.Recommendations.Enums;
using FluentAssertions;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Pruebas de los validadores estructurales de los comandos de revisión y retroalimentación.
/// </summary>
public sealed class RecommendationValidatorsTests
{
    [Fact]
    public void ApproveValidator_ShortNote_Fails()
    {
        var result = new ApproveRecommendationCommandValidator()
            .Validate(new ApproveRecommendationCommand(Guid.NewGuid(), "corta", Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ApproveValidator_ValidNote_Succeeds()
    {
        var result = new ApproveRecommendationCommandValidator()
            .Validate(new ApproveRecommendationCommand(Guid.NewGuid(), "Nota clínica suficientemente larga.", Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RejectValidator_EmptyReason_Fails()
    {
        var result = new RejectRecommendationCommandValidator()
            .Validate(new RejectRecommendationCommand(Guid.NewGuid(), "   ", Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SubmitFeedbackValidator_CommentTooLong_Fails()
    {
        var result = new SubmitFeedbackCommandValidator()
            .Validate(new SubmitFeedbackCommand(Guid.NewGuid(), true, FeedbackOutcome.Improvement, new string('x', 501), Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SubmitFeedbackValidator_NullComment_Succeeds()
    {
        var result = new SubmitFeedbackCommandValidator()
            .Validate(new SubmitFeedbackCommand(Guid.NewGuid(), true, FeedbackOutcome.NoChange, null, Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }
}
