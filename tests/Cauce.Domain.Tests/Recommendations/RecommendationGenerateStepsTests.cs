using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
using FluentAssertions;

namespace Cauce.Domain.Tests.Recommendations;

/// <summary>
/// Pruebas de los pasos de aplicación que recibe <see cref="Recommendation.Generate"/>.
/// </summary>
public sealed class RecommendationGenerateStepsTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private static Recommendation Generate(IReadOnlyList<string>? steps)
    {
        var items = new[] { RecommendationItem.Create(Guid.NewGuid(), ActionType.Reduce, "razón", null) };
        return Recommendation.Generate(
            Guid.NewGuid(), Guid.NewGuid(), ConfidenceScore.Create(0.8m), ExplanationSource.LlmGenerated,
            "explicación", items, Now, TimeSpan.FromHours(72), steps);
    }

    [Fact]
    public void Generate_WithSteps_KeepsThemInOrder()
    {
        var recommendation = Generate(["Primero", "Segundo", "Tercero"]);

        recommendation.Steps.Should().Equal("Primero", "Segundo", "Tercero");
    }

    [Fact]
    public void Generate_WithoutStepsArgument_HasNoSteps()
    {
        var items = new[] { RecommendationItem.Create(Guid.NewGuid(), ActionType.Reduce, "razón", null) };

        var recommendation = Recommendation.Generate(
            Guid.NewGuid(), Guid.NewGuid(), ConfidenceScore.Create(0.8m), ExplanationSource.Fallback,
            null, items, Now, TimeSpan.FromHours(72));

        recommendation.Steps.Should().BeNull();
    }

    [Fact]
    public void Generate_WithEmptyStepList_HasNoSteps()
    {
        Generate([]).Steps.Should().BeNull();
    }

    [Fact]
    public void Generate_WithStepsAndLaterChangesToTheSourceList_KeepsItsOwnCopy()
    {
        var source = new List<string> { "Cocinar la zanahoria al vapor" };

        var recommendation = Generate(source);
        source[0] = "Otro texto";

        recommendation.Steps.Should().Equal("Cocinar la zanahoria al vapor");
    }

    [Fact]
    public void Generate_WithStepOfMaximumLength_Accepts()
    {
        var step = new string('a', RecommendationSteps.MaxLength);

        Generate([step]).Steps.Should().Equal(step);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_WithBlankStep_ThrowsArgumentException(string blank)
    {
        var act = () => Generate(["Paso válido", blank]);

        act.Should().Throw<ArgumentException>().WithParameterName("steps");
    }

    [Fact]
    public void Generate_WithNullStep_ThrowsArgumentException()
    {
        var act = () => Generate(["Paso válido", null!]);

        act.Should().Throw<ArgumentException>().WithParameterName("steps");
    }

    [Fact]
    public void Generate_WithStepOverMaximumLength_ThrowsArgumentException()
    {
        var act = () => Generate([new string('a', RecommendationSteps.MaxLength + 1)]);

        act.Should().Throw<ArgumentException>().WithParameterName("steps");
    }
}
