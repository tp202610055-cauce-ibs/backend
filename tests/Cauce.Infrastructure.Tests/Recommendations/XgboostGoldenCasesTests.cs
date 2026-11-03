using System.Globalization;
using Cauce.Infrastructure.Recommendations.Engines;
using FluentAssertions;
using Xunit.Abstractions;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Prueba dorada: los vectores crudos exportados desde Python pasan por la sesión ONNX del motor, sin el
/// mapper, y deben reproducir la probabilidad con tolerancia 1e-4 y la misma etiqueta.
/// </summary>
public sealed class XgboostGoldenCasesTests : IClassFixture<XgboostEngineFixture>
{
    private const double Tolerance = 0.0001;
    private const float LabelThreshold = 0.5f;

    private static readonly IReadOnlyList<GoldenCase> Cases = XgboostTestData.LoadGoldenCases();

    private readonly XgboostRecommendationEngine _engine;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Inicializa la prueba con el motor compartido.
    /// </summary>
    public XgboostGoldenCasesTests(XgboostEngineFixture fixture, ITestOutputHelper output)
    {
        _engine = fixture.Engine;
        _output = output;
    }

    /// <summary>
    /// Índices de los casos, uno por fila de la prueba.
    /// </summary>
    public static TheoryData<int> CaseIndexes
    {
        get
        {
            var indexes = new TheoryData<int>();
            foreach (var index in Enumerable.Range(0, Cases.Count))
            {
                indexes.Add(index);
            }

            return indexes;
        }
    }

    [Theory]
    [MemberData(nameof(CaseIndexes))]
    public void Predict_GoldenCase_ReproducesPythonOutput(int index)
    {
        var goldenCase = Cases[index];

        var (labels, probabilities) = _engine.Predict(goldenCase.Vector);

        Math.Abs((double)probabilities[0] - goldenCase.ExpectedProbability).Should().BeLessThanOrEqualTo(Tolerance);
        (probabilities[0] >= LabelThreshold ? 1 : 0).Should().Be(goldenCase.ExpectedLabel);
        labels[0].Should().Be(goldenCase.ExpectedLabel);
    }

    [Fact]
    public void Predict_AllGoldenCasesInOneTensor_StayWithinTolerance()
    {
        Cases.Should().HaveCount(25);

        var (_, probabilities) = _engine.Predict(Cases.SelectMany(goldenCase => goldenCase.Vector).ToArray());

        var differences = Cases
            .Select((goldenCase, row) => Math.Abs((double)probabilities[row] - goldenCase.ExpectedProbability))
            .ToList();
        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture, $"Diferencia máxima en {Cases.Count} casos: {differences.Max():E3}"));

        differences.Should().OnlyContain(difference => difference <= Tolerance);
    }
}
