using System.Text.Json;
using System.Text.Json.Serialization;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Infrastructure.Recommendations.Engines;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Datos compartidos de las pruebas del motor XGBoost: los archivos de <c>TestData</c>.
/// </summary>
internal static class XgboostTestData
{
    /// <summary>
    /// SHA-256 del modelo, tomado de su ficha.
    /// </summary>
    public const string ModelSha256 = "eb9da0167ed7dc4f9db2d0f5e0da2b3e9eed0e250f993077c05fc378013f7481";

    /// <summary>
    /// Ruta del modelo copiado al directorio de salida.
    /// </summary>
    public static string ModelPath => TestDataFile("xgboost-sii-v1.2.0.onnx");

    /// <summary>
    /// Crea un motor con las rutas y el hash indicados.
    /// </summary>
    /// <param name="modelPath">Ruta del modelo.</param>
    /// <param name="modelSha256">SHA-256 esperado.</param>
    public static XgboostRecommendationEngine CreateEngine(string modelPath, string modelSha256) =>
        new(
            Options.Create(new RecommendationsOptions
            {
                EngineKind = RecommendationsOptions.XgboostEngineKind,
                XgboostModelPath = modelPath,
                XgboostModelSha256 = modelSha256
            }),
            NullLogger<XgboostRecommendationEngine>.Instance);

    /// <summary>
    /// Carga los casos de prueba exportados junto con el modelo.
    /// </summary>
    public static IReadOnlyList<GoldenCase> LoadGoldenCases()
    {
        using var stream = File.OpenRead(TestDataFile("xgboost-sii-v1.2.0.casos_de_prueba.json"));
        return JsonSerializer.Deserialize<GoldenCaseFile>(stream)!.Cases;
    }

    private static string TestDataFile(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", name);

    private sealed record GoldenCaseFile([property: JsonPropertyName("casos")] IReadOnlyList<GoldenCase> Cases);
}

/// <summary>
/// Caso de prueba exportado desde Python: vector crudo, probabilidad y etiqueta esperadas.
/// </summary>
/// <param name="Vector">Las 10 variables de entrada.</param>
/// <param name="ExpectedProbability">Probabilidad de síntomas esperada.</param>
/// <param name="ExpectedLabel">Etiqueta esperada.</param>
internal sealed record GoldenCase(
    [property: JsonPropertyName("vector")] float[] Vector,
    [property: JsonPropertyName("probabilidad_esperada")] float ExpectedProbability,
    [property: JsonPropertyName("etiqueta_esperada")] int ExpectedLabel);

/// <summary>
/// Motor compartido entre las pruebas de una clase, construido con el modelo real.
/// </summary>
public sealed class XgboostEngineFixture : IDisposable
{
    /// <summary>
    /// Motor con el modelo real.
    /// </summary>
    public XgboostRecommendationEngine Engine { get; } =
        XgboostTestData.CreateEngine(XgboostTestData.ModelPath, XgboostTestData.ModelSha256);

    /// <inheritdoc />
    public void Dispose() => Engine.Dispose();
}
