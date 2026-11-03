using System.Globalization;
using System.Security.Cryptography;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Recommendations.Engines;
using FluentAssertions;
using Xunit.Abstractions;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas del motor XGBoost: validación al construirse, puntuación extremo a extremo con el mapper y
/// resultado sobre el catálogo sembrado.
/// </summary>
public sealed class XgboostRecommendationEngineTests : IClassFixture<XgboostEngineFixture>
{
    private const double EndToEndTolerance = 0.001;

    private static readonly RecommendationsOptions Thresholds = new();

    private static readonly Dictionary<string, PatientContextSnapshot> Patients = new()
    {
        ["P1"] = Patient(35, 24.5m, "SII-D", 3),
        ["P2"] = Patient(52, 29.3m, "SII-C", 10),
        ["P3"] = Patient(28, 21.0m, "SII-M", 1),
        ["P4"] = Patient(61, 33.2m, "SII-U", 15)
    };

    private readonly XgboostRecommendationEngine _engine;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Inicializa la prueba con el motor compartido.
    /// </summary>
    public XgboostRecommendationEngineTests(XgboostEngineFixture fixture, ITestOutputHelper output)
    {
        _engine = fixture.Engine;
        _output = output;
    }

    private static PatientContextSnapshot Patient(int age, decimal bmi, string subtype, int years) =>
        new(age, "Femenino", bmi, subtype, years, "Media", IsSmoker: false, ConsumesAlcohol: false);

    private static CandidateFood Candidate(SeederFood food, int grams) =>
        new(Guid.NewGuid(), food.Name, food.Category, food.Level, 0, 0, 0, 0, grams, food.Tags);

    private static CandidateFood SeededFood(string name, int grams) =>
        Candidate(SeededFoodCatalog.Read().Single(food => food.Name == name), grams);

    private static RecommendationEngineInput Input(PatientContextSnapshot patient, params CandidateFood[] foods) =>
        new(patient, foods, new MealContext("almuerzo"));

    // Replica los umbrales de ClassifyAction, privado en el handler, sin el paso a sustituto.
    private static string Classify(decimal probability)
    {
        if (probability >= Thresholds.AvoidThreshold)
        {
            return "evitar";
        }

        return probability >= Thresholds.ReduceThreshold ? "reducir" : "sugerir";
    }

    private static string WriteTemporaryModel(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.onnx");
        File.WriteAllBytes(path, content);
        return path;
    }

    // ----- Descriptor -----

    [Fact]
    public void Descriptor_WithRealModel_IdentifiesTheRealVersion()
    {
        _engine.Descriptor.VersionName.Should().Be("xgboost-sii-v1.2.0");
        _engine.Descriptor.EngineKind.Should().Be("Xgboost");
        _engine.Descriptor.IsDummy.Should().BeFalse();
        _engine.Descriptor.ModelHash.Should().Be(XgboostTestData.ModelSha256);
    }

    [Fact]
    public void Constructor_ConfiguredHashInUpperCase_IsAccepted()
    {
        using var engine = XgboostTestData.CreateEngine(
            XgboostTestData.ModelPath, XgboostTestData.ModelSha256.ToUpperInvariant());

        engine.Descriptor.VersionName.Should().Be("xgboost-sii-v1.2.0");
    }

    // ----- Fallos al construirse -----

    [Fact]
    public void Constructor_MissingFile_ThrowsNamingTheOption()
    {
        var act = () => XgboostTestData.CreateEngine(
            Path.Combine(Path.GetTempPath(), "no-existe.onnx"), XgboostTestData.ModelSha256);

        act.Should().Throw<InvalidOperationException>().WithMessage("*XgboostModelPath*no existe*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyPath_ThrowsNamingTheOption(string? path)
    {
        var act = () => XgboostTestData.CreateEngine(path!, XgboostTestData.ModelSha256);

        act.Should().Throw<InvalidOperationException>().WithMessage("*XgboostModelPath*obligatoria*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyHash_ThrowsNamingTheOption(string? hash)
    {
        var act = () => XgboostTestData.CreateEngine(XgboostTestData.ModelPath, hash!);

        act.Should().Throw<InvalidOperationException>().WithMessage("*XgboostModelSha256*obligatoria*");
    }

    [Fact]
    public void Constructor_WrongHash_ThrowsShowingBothHashes()
    {
        var wrongHash = new string('0', 64);

        var act = () => XgboostTestData.CreateEngine(XgboostTestData.ModelPath, wrongHash);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*XgboostModelSha256*{wrongHash}*{XgboostTestData.ModelSha256}*");
    }

    [Fact]
    public void Constructor_CorruptFileWithMatchingHash_ThrowsInsteadOfFallingBack()
    {
        var content = RandomNumberGenerator.GetBytes(4096);
        var path = WriteTemporaryModel(content);

        try
        {
            var act = () => XgboostTestData.CreateEngine(path, Convert.ToHexString(SHA256.HashData(content)));

            act.Should().Throw<InvalidOperationException>().WithMessage("*XgboostModelPath*ONNX Runtime*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Constructor_ModelWithAnotherContract_ThrowsDescribingTheExpectedOne()
    {
        var dummyPath = OnnxRecommendationEngine.ResolveModelPath("infrastructure/models/dummy_v0.0.1.onnx")!;
        var dummyHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dummyPath)));

        var act = () => XgboostTestData.CreateEngine(dummyPath, dummyHash);

        act.Should().Throw<InvalidOperationException>().WithMessage("*otro contrato*[N, 10]*");
    }

    // ----- Extremo a extremo -----

    [Theory]
    [InlineData("P1", "Cebolla cocida", 150, 0.8554, "evitar")]
    [InlineData("P1", "Avena en hojuelas cocida", 150, 0.8348, "evitar")]
    [InlineData("P1", "Arroz blanco cocido", 150, 0.3882, "sugerir")]
    [InlineData("P2", "Manzana red delicious", 250, 0.9279, "evitar")]
    [InlineData("P2", "Queso fresco", 100, 0.7254, "evitar")]
    [InlineData("P3", "Leche entera de vaca", 200, 0.7894, "evitar")]
    [InlineData("P3", "Camote sancochado", 300, 0.8625, "evitar")]
    [InlineData("P4", "Pollo a la plancha", 400, 0.5083, "reducir")]
    public async Task ScoreFoodsAsync_ReferenceCase_MatchesProbabilityAndAction(
        string patient, string foodName, int grams, double expectedProbability, string expectedAction)
    {
        var result = await _engine.ScoreFoodsAsync(
            Input(Patients[patient], SeededFood(foodName, grams)), CancellationToken.None);

        var probability = result.Scores.Single().SymptomProbability;
        ((double)probability).Should().BeApproximately(expectedProbability, EndToEndTolerance);
        Classify(probability).Should().Be(expectedAction);
    }

    [Theory]
    [InlineData(100, 12, 14, 0)]
    [InlineData(150, 12, 14, 0)]
    [InlineData(250, 12, 0, 14)]
    public async Task ScoreFoodsAsync_WholeCatalogForP1_ProducesExpectedActions(
        int grams, int avoid, int suggest, int reduce)
    {
        var catalog = SeededFoodCatalog.Read();
        var foods = catalog.Select(food => Candidate(food, grams)).ToArray();

        var result = await _engine.ScoreFoodsAsync(Input(Patients["P1"], foods), CancellationToken.None);

        var actions = result.Scores.Select(score => Classify(score.SymptomProbability)).ToList();
        for (var row = 0; row < foods.Length; row++)
        {
            var (_, raw) = _engine.Predict(XgboostFeatureMapper.Map(Patients["P1"], foods[row], grams));
            _output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{grams}|{foods[row].Name}|{foods[row].FodmapLevel}|{raw[0]:0.0000}|{result.Scores[row].SymptomProbability:0.000}|{actions[row]}"));
        }

        actions.Count(action => action == "evitar").Should().Be(avoid);
        actions.Count(action => action == "sugerir").Should().Be(suggest);
        actions.Count(action => action == "reducir").Should().Be(reduce);
    }

    [Fact]
    public async Task ScoreFoodsAsync_SeveralFoods_MatchesScoringEachOneAlone()
    {
        var foods = new[]
        {
            SeededFood("Cebolla cocida", 150),
            SeededFood("Arroz blanco cocido", 150),
            SeededFood("Queso fresco", 100)
        };

        var together = await _engine.ScoreFoodsAsync(Input(Patients["P1"], foods), CancellationToken.None);

        for (var row = 0; row < foods.Length; row++)
        {
            var alone = await _engine.ScoreFoodsAsync(Input(Patients["P1"], foods[row]), CancellationToken.None);
            together.Scores[row].Should().Be(alone.Scores.Single());
        }
    }

    [Fact]
    public async Task ScoreFoodsAsync_ReportsItselfAsTheModelUsed()
    {
        var result = await _engine.ScoreFoodsAsync(
            Input(Patients["P1"], SeededFood("Cebolla cocida", 150)), CancellationToken.None);

        result.ModelUsed.Should().Be(_engine.Descriptor);
    }

    [Fact]
    public async Task ScoreFoodsAsync_Reasoning_FollowsTheOnnxEnginePattern()
    {
        var result = await _engine.ScoreFoodsAsync(
            Input(Patients["P1"], SeededFood("Cebolla cocida", 150), SeededFood("Arroz blanco cocido", 150)),
            CancellationToken.None);

        result.Scores[0].Reasoning.Should().Be("Riesgo estimado por el modelo alta (probabilidad 0.86).");
        result.Scores[1].Reasoning.Should().Be("Riesgo estimado por el modelo baja (probabilidad 0.39).");
    }

    [Fact]
    public async Task ScoreFoodsAsync_AggregateConfidence_AveragesOnlyActionableFoods()
    {
        var result = await _engine.ScoreFoodsAsync(
            Input(Patients["P1"], SeededFood("Cebolla cocida", 150), SeededFood("Arroz blanco cocido", 150)),
            CancellationToken.None);

        result.AggregateConfidence.Value.Should().Be(result.Scores[0].SymptomProbability);
    }

    [Fact]
    public async Task ScoreFoodsAsync_NoCandidates_ReturnsEmptyScoresWithoutInference()
    {
        var result = await _engine.ScoreFoodsAsync(Input(Patients["P1"]), CancellationToken.None);

        result.Scores.Should().BeEmpty();
        result.AggregateConfidence.Value.Should().Be(0m);
    }

    [Fact]
    public async Task ScoreFoodsAsync_NullInput_Throws()
    {
        var act = () => _engine.ScoreFoodsAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ScoreFoodsAsync_FoodWithZeroQuantity_PropagatesTheMapperFailure()
    {
        var act = () => _engine.ScoreFoodsAsync(
            Input(Patients["P1"], SeededFood("Arroz blanco cocido", 0)), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ScoreFoodsAsync_CancelledToken_Throws()
    {
        var act = () => _engine.ScoreFoodsAsync(
            Input(Patients["P1"], SeededFood("Arroz blanco cocido", 150)), new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ScoreFoodsAsync_AfterDispose_Throws()
    {
        var engine = XgboostTestData.CreateEngine(XgboostTestData.ModelPath, XgboostTestData.ModelSha256);
        engine.Dispose();

        var act = () => engine.ScoreFoodsAsync(
            Input(Patients["P1"], SeededFood("Arroz blanco cocido", 150)), CancellationToken.None);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
