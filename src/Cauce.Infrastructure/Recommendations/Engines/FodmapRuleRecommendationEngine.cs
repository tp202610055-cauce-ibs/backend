using System.Security.Cryptography;
using System.Text;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Recommendations.Engines;

/// <summary>
/// Motor de recomendaciones por regla determinística FODMAP (DEC-B4-01), derivado del dataset de
/// Mirian Contreras. Produce un puntaje en [0, 1] por alimento. Si las cuatro columnas FODMAP
/// granulares no están pobladas, cae a una heurística por nivel FODMAP global.
/// </summary>
public sealed class FodmapRuleRecommendationEngine : IRecommendationEngine
{
    /// <summary>
    /// Cadena versionada que identifica la fórmula. El hash del motor se deriva de ella, no del
    /// archivo fuente, para ser determinista y portable. Si la fórmula cambia, se actualiza esta
    /// constante y el hash refleja el cambio (DEC-B4-10).
    /// </summary>
    private const string RuleVersionString = "fodmap-rule-v1.0.0:127+128o+95f+86p+105l+55q";

    private const decimal Intercept = 0.127m;
    private const decimal OligosCoefficient = 0.128m;
    private const decimal FructoseCoefficient = 0.095m;
    private const decimal PolyolsCoefficient = 0.086m;
    private const decimal LactoseCoefficient = 0.105m;
    private const decimal QuantityCoefficient = 0.00055m;

    private readonly RecommendationsOptions _options;
    private readonly ModelVersionDescriptor _descriptor;
    private readonly ILogger<FodmapRuleRecommendationEngine> _logger;

    /// <summary>
    /// Inicializa el motor con sus dependencias.
    /// </summary>
    /// <param name="options">Opciones del módulo de recomendaciones.</param>
    /// <param name="logger">Logger de la categoría del motor.</param>
    public FodmapRuleRecommendationEngine(
        IOptions<RecommendationsOptions> options,
        ILogger<FodmapRuleRecommendationEngine> logger)
    {
        _options = options.Value;
        _logger = logger;
        _descriptor = new ModelVersionDescriptor("rule-v1.0.0", ComputeRuleHash(), "Rule");
    }

    /// <inheritdoc />
    public ModelVersionDescriptor Descriptor => _descriptor;

    /// <inheritdoc />
    public Task<RecommendationEngineResult> ScoreFoodsAsync(
        RecommendationEngineInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var scores = new List<FoodScore>(input.CandidateFoods.Count);
        foreach (var food in input.CandidateFoods)
        {
            var probability = ScoreFood(food);
            scores.Add(new FoodScore(food.FoodId, probability, BuildReasoning(probability)));
        }

        var confidence = ComputeAggregateConfidence(scores);

        _logger.LogDebug(
            "Rule engine scored {Count} candidate foods with aggregate confidence {Confidence}.",
            scores.Count,
            confidence.Value);

        return Task.FromResult(new RecommendationEngineResult(scores, confidence, _descriptor));
    }

    private decimal ScoreFood(CandidateFood food)
    {
        decimal rawScore;
        var granularPopulated = food.OligosLevel != 0 || food.FructoseLevel != 0
            || food.PolyolsLevel != 0 || food.LactoseLevel != 0;

        if (granularPopulated)
        {
            rawScore = Intercept
                + (OligosCoefficient * food.OligosLevel)
                + (FructoseCoefficient * food.FructoseLevel)
                + (PolyolsCoefficient * food.PolyolsLevel)
                + (LactoseCoefficient * food.LactoseLevel)
                + (QuantityCoefficient * food.QuantityGrams);
        }
        else
        {
            // Catálogo no enriquecido: heurística por nivel FODMAP global.
            rawScore = food.FodmapLevel switch
            {
                FodmapLevel.High => 0.80m,
                FodmapLevel.Moderate => 0.50m,
                FodmapLevel.Low => 0.25m,
                _ => 0.30m
            };
        }

        var clamped = Math.Clamp(rawScore, 0.0m, 1.0m);
        return Math.Round(clamped, 3, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Calcula la confianza agregada como el promedio de las probabilidades de los ítems
    /// accionables (acción <c>Avoid</c> o <c>Reduce</c>, es decir, puntaje mayor o igual al umbral
    /// de reducción). Si no hay ítems accionables, la confianza es cero. La confianza debe medirse
    /// sobre las recomendaciones que mueven el comportamiento del paciente, no sobre las de baja
    /// probabilidad (aclaración técnica del Bloque 4).
    /// </summary>
    private ConfidenceScore ComputeAggregateConfidence(IReadOnlyList<FoodScore> scores)
    {
        var actionable = scores
            .Where(score => score.SymptomProbability >= _options.ReduceThreshold)
            .Select(score => score.SymptomProbability)
            .ToList();

        var aggregate = actionable.Count == 0 ? 0m : actionable.Average();
        return ConfidenceScore.Create(aggregate);
    }

    private string BuildReasoning(decimal probability)
    {
        var level = probability >= _options.AvoidThreshold
            ? "alta"
            : probability >= _options.ReduceThreshold
                ? "moderada"
                : "baja";

        return $"Carga FODMAP estimada {level} (probabilidad {probability.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}).";
    }

    private static string ComputeRuleHash()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(RuleVersionString));
        return Convert.ToHexString(hash);
    }
}
