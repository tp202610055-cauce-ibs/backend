using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Recommendations.Engines;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas del motor por regla FODMAP, con foco en el cálculo de la confianza agregada
/// (aclaración técnica del Bloque 4) y en el rango de los puntajes.
/// </summary>
public sealed class FodmapRuleRecommendationEngineTests
{
    private static FodmapRuleRecommendationEngine Engine() =>
        new(Options.Create(new RecommendationsOptions()), NullLogger<FodmapRuleRecommendationEngine>.Instance);

    private static CandidateFood Food(FodmapLevel level, byte granular = 0) =>
        new(Guid.NewGuid(), "Alimento", "categoria", level, granular, granular, granular, granular, 100);

    private static RecommendationEngineInput Input(params CandidateFood[] foods) =>
        new(new PatientContextSnapshot(30, "Masculino", 22m, "SII-M", 1, "Media", false, false), foods, new MealContext("almuerzo"));

    [Fact]
    public async Task ScoreFoodsAsync_ProducesScoresWithinRange()
    {
        var result = await Engine().ScoreFoodsAsync(Input(Food(FodmapLevel.High, granular: 2), Food(FodmapLevel.Low)), CancellationToken.None);

        result.Scores.Should().OnlyContain(score => score.SymptomProbability >= 0m && score.SymptomProbability <= 1m);
    }

    [Fact]
    public void Descriptor_HasRuleVersionAndHash()
    {
        var descriptor = Engine().Descriptor;

        descriptor.VersionName.Should().Be("rule-v1.0.0");
        descriptor.EngineKind.Should().Be("Rule");
        descriptor.ModelHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AggregateConfidence_WithOnlySuggestItems_IsZero()
    {
        var result = await Engine().ScoreFoodsAsync(Input(Food(FodmapLevel.Low), Food(FodmapLevel.Low)), CancellationToken.None);

        result.AggregateConfidence.Value.Should().Be(0m);
    }

    [Fact]
    public async Task AggregateConfidence_WithOnlyAvoidItems_AveragesAllOfThem()
    {
        var result = await Engine().ScoreFoodsAsync(Input(Food(FodmapLevel.High), Food(FodmapLevel.High)), CancellationToken.None);

        result.AggregateConfidence.Value.Should().Be(0.80m);
    }

    [Fact]
    public async Task AggregateConfidence_WithMixedActions_AveragesOnlyAvoidAndReduce()
    {
        // High => 0.80 (Avoid), Moderate => 0.50 (Reduce), Low => 0.25 (Suggest, excluido).
        var result = await Engine().ScoreFoodsAsync(
            Input(Food(FodmapLevel.High), Food(FodmapLevel.Moderate), Food(FodmapLevel.Low)),
            CancellationToken.None);

        result.AggregateConfidence.Value.Should().Be(0.65m);
    }
}
