using System.Globalization;
using System.Security.Cryptography;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Recommendations.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Cauce.Infrastructure.Recommendations.Engines;

/// <summary>
/// Motor respaldado por el modelo real xgboost-sii-v1.2.0, exportado a ONNX. Valida el archivo al construirse
/// y cualquier fallo se propaga: nunca cae en silencio al motor de regla. La sesión admite llamadas concurrentes.
/// </summary>
public sealed class XgboostRecommendationEngine : IRecommendationEngine, IDisposable
{
    /// <summary>
    /// Nombre de la versión del modelo.
    /// </summary>
    public const string ModelVersionName = "xgboost-sii-v1.2.0";

    private const string InputName = "input";
    private const string LabelOutputName = "label";
    private const string ProbabilitiesOutputName = "probabilities";
    private const int ClassCount = 2;
    private const int SymptomClassIndex = 1;

    private readonly RecommendationsOptions _options;
    private readonly ILogger<XgboostRecommendationEngine> _logger;
    private readonly InferenceSession _session;
    private readonly ModelVersionDescriptor _descriptor;
    private bool _disposed;

    /// <summary>
    /// Inicializa el motor: comprueba el archivo y su hash, carga la sesión y valida las entradas y salidas.
    /// </summary>
    /// <param name="options">Opciones del módulo de recomendaciones.</param>
    /// <param name="logger">Logger de la categoría del motor.</param>
    /// <exception cref="InvalidOperationException">Si el archivo, el hash o el modelo no son válidos.</exception>
    public XgboostRecommendationEngine(
        IOptions<RecommendationsOptions> options,
        ILogger<XgboostRecommendationEngine> logger)
    {
        _options = options.Value;
        _logger = logger;

        var modelPath = ResolveModelPath(_options.XgboostModelPath);
        var modelHash = VerifyModelHash(modelPath, _options.XgboostModelSha256);

        _session = LoadSession(modelPath);
        try
        {
            ValidateModelContract(_session, modelPath);
        }
        catch
        {
            _session.Dispose();
            throw;
        }

        _descriptor = new ModelVersionDescriptor(
            ModelVersionName, modelHash, RecommendationsOptions.XgboostEngineKind, IsDummy: false);
        _logger.LogInformation("XGBoost model '{Version}' loaded from '{Path}'.", ModelVersionName, modelPath);
    }

    /// <inheritdoc />
    public ModelVersionDescriptor Descriptor => _descriptor;

    /// <inheritdoc />
    public Task<RecommendationEngineResult> ScoreFoodsAsync(
        RecommendationEngineInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var foods = input.CandidateFoods;
        var scores = new List<FoodScore>(foods.Count);

        if (foods.Count > 0)
        {
            var (_, probabilities) = Predict(BuildFeatures(input.Patient, foods));
            for (var row = 0; row < foods.Count; row++)
            {
                var probability = ToScore(probabilities[row]);
                scores.Add(new FoodScore(foods[row].FoodId, probability, BuildReasoning(probability)));
            }
        }

        var confidence = ComputeAggregateConfidence(scores);
        _logger.LogDebug(
            "XGBoost engine scored {Count} candidate foods with aggregate confidence {Confidence}.",
            scores.Count,
            confidence.Value);

        return Task.FromResult(new RecommendationEngineResult(scores, confidence, _descriptor));
    }

    /// <summary>
    /// Corre una sola inferencia sobre un tensor [N, 10] y lee las salidas por nombre.
    /// </summary>
    /// <param name="features">Las N filas de 10 variables, una detrás de otra.</param>
    /// <returns>La etiqueta del modelo y la probabilidad de síntomas de cada fila.</returns>
    internal (long[] Labels, float[] Probabilities) Predict(float[] features)
    {
        var rowCount = features.Length / XgboostFeatureMapper.FeatureCount;
        var tensor = new DenseTensor<float>(features, new[] { rowCount, XgboostFeatureMapper.FeatureCount });

        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(InputName, tensor) });

        var labelOutput = results.First(value => value.Name == LabelOutputName).AsTensor<long>();
        var probabilityOutput = results.First(value => value.Name == ProbabilitiesOutputName).AsTensor<float>();

        var labels = new long[rowCount];
        var probabilities = new float[rowCount];
        for (var row = 0; row < rowCount; row++)
        {
            labels[row] = labelOutput[row];
            probabilities[row] = probabilityOutput[row, SymptomClassIndex];
        }

        return (labels, probabilities);
    }

    private static float[] BuildFeatures(PatientContextSnapshot patient, IReadOnlyList<CandidateFood> foods)
    {
        var features = new float[foods.Count * XgboostFeatureMapper.FeatureCount];
        for (var row = 0; row < foods.Count; row++)
        {
            var food = foods[row];
            XgboostFeatureMapper
                .Map(patient, food, food.QuantityGrams)
                .CopyTo(features, row * XgboostFeatureMapper.FeatureCount);
        }

        return features;
    }

    private static decimal ToScore(float probability)
    {
        return Math.Round(Math.Clamp((decimal)probability, 0m, 1m), 3, MidpointRounding.AwayFromZero);
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

        var formatted = probability.ToString("0.00", CultureInfo.InvariantCulture);
        return $"Riesgo estimado por el modelo {level} (probabilidad {formatted}).";
    }

    private static string ResolveModelPath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                "Recommendations:XgboostModelPath es obligatoria cuando EngineKind es 'Xgboost'.");
        }

        return OnnxRecommendationEngine.ResolveModelPath(configuredPath)
            ?? throw new InvalidOperationException(
                $"Recommendations:XgboostModelPath apunta a un archivo que no existe: '{configuredPath}'.");
    }

    private static string VerifyModelHash(string modelPath, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            throw new InvalidOperationException(
                "Recommendations:XgboostModelSha256 es obligatoria cuando EngineKind es 'Xgboost'.");
        }

        var actualHash = ComputeFileHash(modelPath);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Recommendations:XgboostModelSha256 no coincide con '{modelPath}': " +
                $"se esperaba {expectedHash} y el archivo tiene {actualHash}.");
        }

        return actualHash;
    }

    private static string ComputeFileHash(string modelPath)
    {
        try
        {
            using var stream = File.OpenRead(modelPath);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Recommendations:XgboostModelPath apunta a un archivo que no se pudo leer: '{modelPath}'.", exception);
        }
    }

    private static InferenceSession LoadSession(string modelPath)
    {
        try
        {
            return new InferenceSession(modelPath);
        }
        catch (OnnxRuntimeException exception)
        {
            throw new InvalidOperationException(
                $"Recommendations:XgboostModelPath apunta a un archivo que ONNX Runtime no pudo cargar: '{modelPath}'.",
                exception);
        }
    }

    private static void ValidateModelContract(InferenceSession session, string modelPath)
    {
        var hasValidInput = session.InputMetadata.TryGetValue(InputName, out var input)
            && IsTensorOf<float>(input)
            && input.Dimensions is [_, XgboostFeatureMapper.FeatureCount];
        var hasValidLabel = session.OutputMetadata.TryGetValue(LabelOutputName, out var label)
            && IsTensorOf<long>(label);
        var hasValidProbabilities = session.OutputMetadata.TryGetValue(ProbabilitiesOutputName, out var probabilities)
            && IsTensorOf<float>(probabilities)
            && probabilities.Dimensions is [_, ClassCount];

        if (!hasValidInput || !hasValidLabel || !hasValidProbabilities)
        {
            throw new InvalidOperationException(
                $"Recommendations:XgboostModelPath apunta a un modelo con otro contrato: '{modelPath}'. Se esperaba " +
                $"la entrada '{InputName}' float [N, {XgboostFeatureMapper.FeatureCount}] y las salidas " +
                $"'{LabelOutputName}' int64 [N] y '{ProbabilitiesOutputName}' float [N, {ClassCount}].");
        }
    }

    private static bool IsTensorOf<TElement>(NodeMetadata metadata)
    {
        return metadata.IsTensor && metadata.ElementType == typeof(TElement);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _session.Dispose();
    }
}
