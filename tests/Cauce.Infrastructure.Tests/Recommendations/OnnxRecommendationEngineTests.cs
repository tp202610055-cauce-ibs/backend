using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Recommendations.Engines;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas del motor ONNX (TS07) con el modelo dummy: carga el modelo, ejecuta la inferencia y produce
/// puntajes válidos. El modelo se resuelve buscando <c>infrastructure/models/dummy_v0.0.1.onnx</c> hacia
/// arriba desde el directorio de ejecución.
/// </summary>
public sealed class OnnxRecommendationEngineTests
{
    private static OnnxRecommendationEngine CreateEngine()
    {
        var options = Options.Create(new RecommendationsOptions { EngineKind = "Onnx" });
        var fallback = new FodmapRuleRecommendationEngine(options, NullLogger<FodmapRuleRecommendationEngine>.Instance);
        return new OnnxRecommendationEngine(options, fallback, NullLogger<OnnxRecommendationEngine>.Instance);
    }

    [Fact]
    public void Descriptor_WithDummyModel_IsOnnxAndDummy()
    {
        using var engine = CreateEngine();

        engine.Descriptor.EngineKind.Should().Be("Onnx");
        engine.Descriptor.IsDummy.Should().BeTrue();
        engine.Descriptor.VersionName.Should().Be("dummy-v0.0.1");
        engine.Descriptor.ModelHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ScoreFoodsAsync_WithDummyModel_ProducesValidScores()
    {
        using var engine = CreateEngine();
        var input = new RecommendationEngineInput(
            new PatientContextSnapshot(30, "Femenino", 22.5m, "SII-D", 2, "Media", IsSmoker: false, ConsumesAlcohol: false),
            new[]
            {
                new CandidateFood(Guid.NewGuid(), "Manzana", "Frutas", FodmapLevel.High, 2, 2, 1, 0, 120),
                new CandidateFood(Guid.NewGuid(), "Arroz", "Cereales", FodmapLevel.Low, 0, 0, 0, 0, 150)
            },
            new MealContext("almuerzo"));

        var result = await engine.ScoreFoodsAsync(input, CancellationToken.None);

        result.Scores.Should().HaveCount(2);
        result.Scores.Should().OnlyContain(score => score.SymptomProbability >= 0m && score.SymptomProbability <= 1m);
        result.ModelUsed.EngineKind.Should().Be("Onnx");
        result.ModelUsed.IsDummy.Should().BeTrue();
    }

    [Fact]
    public void ResolveModelPath_MissingRelativePath_ReturnsNull()
    {
        OnnxRecommendationEngine.ResolveModelPath("does/not/exist/model.onnx").Should().BeNull();
    }
}
