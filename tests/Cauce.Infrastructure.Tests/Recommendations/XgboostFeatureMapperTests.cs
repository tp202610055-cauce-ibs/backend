using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Recommendations.Engines;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas del mapeo a las 10 variables de xgboost-sii-v1.2.0: orden del contrato, tabla de verdad de nivel
/// y etiquetas, subtipos, años de diagnóstico y entradas inválidas.
/// </summary>
public sealed class XgboostFeatureMapperTests
{
    private const int SubtypeIndex = 2;
    private const int YearsIndex = 3;
    private const int LevelIndex = 4;
    private const int GroupCountWithLevel = 5;

    private static readonly PatientContextSnapshot DefaultPatient = Patient();

    // Vector (nivel, oligos, fructosa, polioles, lactosa) esperado, fijo y escrito a mano: no sale del mapper.
    private static readonly Dictionary<string, float[]> NonLowFoods = new()
    {
        ["Pan de trigo blanco"] = [0f, 2f, 0f, 0f, 0f],
        ["Leche entera de vaca"] = [0f, 0f, 0f, 0f, 2f],
        ["Manzana red delicious"] = [0f, 2f, 0f, 2f, 0f],
        ["Cebolla cocida"] = [0f, 2f, 0f, 0f, 0f],
        ["Lentejas cocidas"] = [0f, 2f, 0f, 0f, 0f],
        ["Frejol canario cocido"] = [0f, 2f, 0f, 0f, 0f],
        ["Avena en hojuelas cocida"] = [0f, 1f, 0f, 0f, 0f],
        ["Queso fresco"] = [0f, 0f, 0f, 0f, 1f],
        ["Camote sancochado"] = [0f, 0f, 0f, 1f, 0f],
        ["Brócoli cocido"] = [0f, 1f, 0f, 0f, 0f],
        ["Aji de gallina"] = [0f, 0f, 0f, 0f, 1f],
        ["Lomo saltado"] = [0f, 1f, 0f, 0f, 0f]
    };

    private static readonly float[] LowFood = [1f, 0f, 0f, 0f, 0f];

    private static PatientContextSnapshot Patient(
        int age = 35, decimal bmi = 24.5m, string subtype = "SII-D", int years = 3) =>
        new(age, "Femenino", bmi, subtype, years, "Media", IsSmoker: false, ConsumesAlcohol: false);

    private static CandidateFood Food(FodmapLevel level, string? tags) =>
        new(Guid.NewGuid(), "Alimento", "categoria", level, 0, 0, 0, 0, 150, tags);

    [Fact]
    public void Map_ValidInput_ReturnsTenFeaturesInContractOrder()
    {
        var features = XgboostFeatureMapper.Map(
            Patient(52, 29.25m, "SII-C", 10), Food(FodmapLevel.High, "fructose,lactose"), 250f);

        features.Should().Equal(52f, 29.25f, 0f, 10f, 0f, 0f, 2f, 0f, 2f, 250f);
    }

    [Theory]
    [InlineData(FodmapLevel.High, null, 0f, 0f, 0f, 0f, 0f)]
    [InlineData(FodmapLevel.High, "fructans", 0f, 2f, 0f, 0f, 0f)]
    [InlineData(FodmapLevel.High, "oligos", 0f, 2f, 0f, 0f, 0f)]
    [InlineData(FodmapLevel.High, "fructose", 0f, 0f, 2f, 0f, 0f)]
    [InlineData(FodmapLevel.High, "polyols", 0f, 0f, 0f, 2f, 0f)]
    [InlineData(FodmapLevel.High, "lactose", 0f, 0f, 0f, 0f, 2f)]
    [InlineData(FodmapLevel.High, "fructans,polyols", 0f, 2f, 0f, 2f, 0f)]
    [InlineData(FodmapLevel.Moderate, null, 0f, 0f, 0f, 0f, 0f)]
    [InlineData(FodmapLevel.Moderate, "fructans", 0f, 1f, 0f, 0f, 0f)]
    [InlineData(FodmapLevel.Moderate, "oligos", 0f, 1f, 0f, 0f, 0f)]
    [InlineData(FodmapLevel.Moderate, "fructose", 0f, 0f, 1f, 0f, 0f)]
    [InlineData(FodmapLevel.Moderate, "polyols", 0f, 0f, 0f, 1f, 0f)]
    [InlineData(FodmapLevel.Moderate, "lactose", 0f, 0f, 0f, 0f, 1f)]
    [InlineData(FodmapLevel.Moderate, "fructans,polyols", 0f, 1f, 0f, 1f, 0f)]
    [InlineData(FodmapLevel.Low, null, 1f, 0f, 0f, 0f, 0f)]
    [InlineData(FodmapLevel.Low, "fructans", 1f, 1f, 0f, 0f, 0f)]
    [InlineData(FodmapLevel.Low, "oligos", 1f, 1f, 0f, 0f, 0f)]
    [InlineData(FodmapLevel.Low, "fructose", 1f, 0f, 1f, 0f, 0f)]
    [InlineData(FodmapLevel.Low, "polyols", 1f, 0f, 0f, 1f, 0f)]
    [InlineData(FodmapLevel.Low, "lactose", 1f, 0f, 0f, 0f, 1f)]
    [InlineData(FodmapLevel.Low, "fructans,polyols", 1f, 1f, 0f, 1f, 0f)]
    public void Map_LevelAndTags_ProducesExpectedLevelAndGroups(
        FodmapLevel level, string? tags, float modelLevel, float oligos, float fructose, float polyols, float lactose)
    {
        var features = XgboostFeatureMapper.Map(DefaultPatient, Food(level, tags), 150f);

        features.Skip(LevelIndex).Take(GroupCountWithLevel).Should().Equal(modelLevel, oligos, fructose, polyols, lactose);
    }

    [Theory]
    [InlineData(" FRUCTANS , Polyols ")]
    [InlineData("fructans,POLYOLS")]
    [InlineData("polyols,fructans,polyols")]
    public void Map_TagsWithSpacesCaseOrRepeats_AreNormalized(string tags)
    {
        var features = XgboostFeatureMapper.Map(DefaultPatient, Food(FodmapLevel.High, tags), 150f);

        features.Skip(LevelIndex).Take(GroupCountWithLevel).Should().Equal(0f, 2f, 0f, 2f, 0f);
    }

    [Fact]
    public void Map_GranularColumns_AreIgnoredInFavorOfTags()
    {
        var food = new CandidateFood(Guid.NewGuid(), "Alimento", "categoria", FodmapLevel.High, 2, 2, 2, 2, 150);

        var features = XgboostFeatureMapper.Map(DefaultPatient, food, 150f);

        features.Skip(LevelIndex).Take(GroupCountWithLevel).Should().Equal(0f, 0f, 0f, 0f, 0f);
    }

    [Fact]
    public void Map_EverySeededFood_MatchesFixedTable()
    {
        var catalog = SeededFoodCatalog.Read();

        catalog.Should().HaveCount(26);
        catalog.Where(food => food.Level == FodmapLevel.Low).Should().HaveCount(14);
        catalog.Select(food => food.Name).Should().Contain(NonLowFoods.Keys);

        foreach (var food in catalog)
        {
            var candidate = new CandidateFood(
                Guid.NewGuid(), food.Name, food.Category, food.Level, 0, 0, 0, 0, 150, food.Tags);

            var features = XgboostFeatureMapper.Map(DefaultPatient, candidate, 150f);

            features.Skip(LevelIndex).Take(GroupCountWithLevel)
                .Should().Equal(NonLowFoods.GetValueOrDefault(food.Name, LowFood), food.Name);
        }
    }

    [Theory]
    [InlineData("SII-C", 0f)]
    [InlineData("SII-D", 1f)]
    [InlineData("SII-M", 2f)]
    [InlineData("SII-U", 3f)]
    [InlineData(" sii-d ", 1f)]
    public void Map_Subtype_IsTranslatedByMeaning(string subtype, float expected)
    {
        var features = XgboostFeatureMapper.Map(Patient(subtype: subtype), Food(FodmapLevel.Low, null), 150f);

        features[SubtypeIndex].Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Map_PatientWithoutSubtype_UsesUnclassified(string? subtype)
    {
        var features = XgboostFeatureMapper.Map(Patient(subtype: subtype!), Food(FodmapLevel.Low, null), 150f);

        features[SubtypeIndex].Should().Be(3f);
    }

    [Theory]
    [InlineData("SII-X")]
    [InlineData("IbsD")]
    [InlineData("1")]
    public void Map_UnknownSubtype_Throws(string subtype)
    {
        var act = () => XgboostFeatureMapper.Map(Patient(subtype: subtype), Food(FodmapLevel.Low, null), 150f);

        act.Should().Throw<ArgumentException>().WithMessage("*subtipo*");
    }

    [Fact]
    public void Map_ZeroYearsSinceDiagnosis_UsesTrainingMedian()
    {
        var features = XgboostFeatureMapper.Map(Patient(years: 0), Food(FodmapLevel.Low, null), 150f);

        features[YearsIndex].Should().Be(3f);
    }

    [Theory]
    [InlineData(null, 3f)]
    [InlineData(0, 3f)]
    [InlineData(1, 1f)]
    [InlineData(15, 15f)]
    public void ResolveYearsSinceDiagnosis_ReturnsValueOrTrainingMedian(int? years, float expected)
    {
        XgboostFeatureMapper.ResolveYearsSinceDiagnosis(years).Should().Be(expected);
    }

    [Fact]
    public void Map_NegativeYearsSinceDiagnosis_Throws()
    {
        var act = () => XgboostFeatureMapper.Map(Patient(years: -1), Food(FodmapLevel.Low, null), 150f);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Map_AgeAndBmiOutsideTrainingRange_AreNotClamped()
    {
        var features = XgboostFeatureMapper.Map(Patient(age: 90, bmi: 45.5m), Food(FodmapLevel.Low, null), 150f);

        features.Take(2).Should().Equal(90f, 45.5f);
    }

    [Fact]
    public void Map_NullPatient_Throws()
    {
        var act = () => XgboostFeatureMapper.Map(null!, Food(FodmapLevel.Low, null), 150f);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Map_NullFood_Throws()
    {
        var act = () => XgboostFeatureMapper.Map(DefaultPatient, null!, 150f);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Map_NonPositiveAge_Throws(int age)
    {
        var act = () => XgboostFeatureMapper.Map(Patient(age: age), Food(FodmapLevel.Low, null), 150f);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*edad*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Map_NonPositiveBmi_Throws(int bmi)
    {
        var act = () => XgboostFeatureMapper.Map(Patient(bmi: bmi), Food(FodmapLevel.Low, null), 150f);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*IMC*");
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Map_InvalidQuantity_Throws(float quantity)
    {
        var act = () => XgboostFeatureMapper.Map(DefaultPatient, Food(FodmapLevel.Low, null), quantity);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*cantidad*");
    }

    [Theory]
    [InlineData("gluten")]
    [InlineData("fructans,gluten")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fructans,")]
    [InlineData(",polyols")]
    [InlineData("fructans,,polyols")]
    [InlineData("fructans;polyols")]
    public void Map_UnknownOrEmptyTag_Throws(string tags)
    {
        var act = () => XgboostFeatureMapper.Map(DefaultPatient, Food(FodmapLevel.High, tags), 150f);

        act.Should().Throw<ArgumentException>().WithMessage("*etiqueta FODMAP desconocida*");
    }

    [Fact]
    public void Map_UnknownFodmapLevel_Throws()
    {
        var act = () => XgboostFeatureMapper.Map(DefaultPatient, Food((FodmapLevel)99, null), 150f);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*nivel FODMAP*");
    }
}
