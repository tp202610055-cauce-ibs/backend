using System.Globalization;
using System.Security.Cryptography;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Cauce.Infrastructure.Recommendations.Engines;

/// <summary>
/// Motor de recomendaciones respaldado por un modelo ONNX (TS07). Carga el modelo de la ruta
/// configurada (<c>Recommendations:OnnxModelPath</c>) y, por cada alimento candidato, combina la
/// tendencia de riesgo del paciente (inferida del modelo con 5 features de contexto) con el nivel FODMAP
/// del alimento para producir un puntaje en [0, 1]. Si el archivo del modelo no existe, cae de forma
/// silenciosa al motor de regla (<see cref="FodmapRuleRecommendationEngine"/>) sin bloquear nada. El
/// modelo dummy es un placeholder swap-ready por el modelo real de Mirian Contreras (acta A23).
/// </summary>
public sealed class OnnxRecommendationEngine : IRecommendationEngine, IDisposable
{
    private const int FeatureCount = 5;
    private const int AvoidClassIndex = 2; // 0=suggest, 1=reduce, 2=avoid

    private readonly FodmapRuleRecommendationEngine _fallback;
    private readonly ILogger<OnnxRecommendationEngine> _logger;
    private readonly InferenceSession? _session;
    private readonly string? _inputName;
    private readonly string? _probabilityOutputName;
    private readonly ModelVersionDescriptor _descriptor;
    private readonly RecommendationsOptions _options;

    /// <summary>
    /// Inicializa el motor cargando el modelo ONNX si está disponible; de lo contrario, delega en el
    /// motor de regla.
    /// </summary>
    /// <param name="options">Opciones del módulo de recomendaciones.</param>
    /// <param name="fallback">Motor de regla usado como respaldo cuando el modelo no está disponible.</param>
    /// <param name="logger">Logger de la categoría del motor.</param>
    public OnnxRecommendationEngine(
        IOptions<RecommendationsOptions> options,
        FodmapRuleRecommendationEngine fallback,
        ILogger<OnnxRecommendationEngine> logger)
    {
        _options = options.Value;
        _fallback = fallback;
        _logger = logger;

        var resolvedPath = ResolveModelPath(_options.OnnxModelPath);
        if (resolvedPath is null)
        {
            _logger.LogWarning(
                "ONNX model not found at '{ConfiguredPath}'. Falling back to the rule engine.",
                _options.OnnxModelPath);
            _descriptor = _fallback.Descriptor;
            return;
        }

        _session = new InferenceSession(resolvedPath);
        _inputName = _session.InputMetadata.Keys.First();
        _probabilityOutputName = _session.OutputMetadata
            .First(output => output.Value.ElementType == typeof(float)).Key;

        var version = Path.GetFileNameWithoutExtension(resolvedPath).Replace('_', '-');
        _descriptor = new ModelVersionDescriptor(version, ComputeFileHash(resolvedPath), "Onnx", IsDummy: true);
        _logger.LogInformation("ONNX model '{Version}' loaded from '{Path}'.", version, resolvedPath);
    }

    /// <inheritdoc />
    public ModelVersionDescriptor Descriptor => _descriptor;

    /// <inheritdoc />
    public Task<RecommendationEngineResult> ScoreFoodsAsync(
        RecommendationEngineInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (_session is null)
        {
            return _fallback.ScoreFoodsAsync(input, cancellationToken);
        }

        var patientRisk = InferPatientRisk(input.Patient);

        var scores = new List<FoodScore>(input.CandidateFoods.Count);
        foreach (var food in input.CandidateFoods)
        {
            var probability = ScoreFood(patientRisk, food.FodmapLevel);
            scores.Add(new FoodScore(food.FoodId, probability, BuildReasoning(probability)));
        }

        var confidence = ComputeAggregateConfidence(scores);
        _logger.LogDebug(
            "ONNX engine scored {Count} candidate foods with aggregate confidence {Confidence}.",
            scores.Count,
            confidence.Value);

        return Task.FromResult(new RecommendationEngineResult(scores, confidence, _descriptor));
    }

    /// <summary>
    /// Corre el modelo con las 5 features del contexto del paciente y devuelve la probabilidad de la
    /// clase "avoid" como tendencia de riesgo del paciente en [0, 1].
    /// </summary>
    /// <param name="patient">Instantánea del contexto del paciente.</param>
    /// <returns>La probabilidad de riesgo del paciente.</returns>
    private decimal InferPatientRisk(PatientContextSnapshot patient)
    {
        var features = new DenseTensor<float>(new[] { 1, FeatureCount });
        features[0, 0] = patient.AgeYears;
        features[0, 1] = (float)patient.BodyMassIndex;
        features[0, 2] = patient.YearsSinceDiagnosis;
        features[0, 3] = patient.IsSmoker ? 1f : 0f;
        features[0, 4] = patient.ConsumesAlcohol ? 1f : 0f;

        var inputs = new[] { NamedOnnxValue.CreateFromTensor(_inputName!, features) };
        using var results = _session!.Run(inputs);

        var probabilities = results.First(value => value.Name == _probabilityOutputName).AsTensor<float>();
        var avoidProbability = probabilities.Dimensions[^1] > AvoidClassIndex
            ? probabilities[0, AvoidClassIndex]
            : probabilities[0, probabilities.Dimensions[^1] - 1];

        return Math.Clamp((decimal)avoidProbability, 0m, 1m);
    }

    /// <summary>
    /// Combina la tendencia de riesgo del paciente con el nivel FODMAP del alimento para obtener el
    /// puntaje del alimento. Semántica dummy pendiente de definición por Mirian (acta A23).
    /// </summary>
    /// <param name="patientRisk">Tendencia de riesgo del paciente en [0, 1].</param>
    /// <param name="fodmapLevel">Nivel FODMAP global del alimento.</param>
    /// <returns>El puntaje del alimento en [0, 1].</returns>
    private static decimal ScoreFood(decimal patientRisk, FodmapLevel fodmapLevel)
    {
        var fodmapWeight = fodmapLevel switch
        {
            FodmapLevel.High => 0.80m,
            FodmapLevel.Moderate => 0.50m,
            FodmapLevel.Low => 0.25m,
            _ => 0.30m
        };

        var combined = (patientRisk * 0.5m) + (fodmapWeight * 0.5m);
        return Math.Round(Math.Clamp(combined, 0m, 1m), 3, MidpointRounding.AwayFromZero);
    }

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

        return $"Riesgo estimado por el modelo {level} (probabilidad {probability.ToString("0.00", CultureInfo.InvariantCulture)}).";
    }

    /// <summary>
    /// Resuelve la ruta del modelo: si es absoluta y existe, la usa; si es relativa, la busca hacia
    /// arriba desde el directorio de ejecución. Devuelve <see langword="null"/> si no la encuentra.
    /// </summary>
    /// <param name="configuredPath">Ruta configurada (absoluta o relativa).</param>
    /// <returns>La ruta absoluta resuelta, o <see langword="null"/>.</returns>
    internal static string? ResolveModelPath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return File.Exists(configuredPath) ? configuredPath : null;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, configuredPath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return File.Exists(configuredPath) ? Path.GetFullPath(configuredPath) : null;
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _session?.Dispose();
    }
}
