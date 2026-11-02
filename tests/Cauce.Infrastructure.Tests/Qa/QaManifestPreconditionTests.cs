using System.Text.Json;
using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Qa.Manifest;
using Cauce.Infrastructure.Qa.Seeding;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Qa;

/// <summary>
/// Precondiciones de la hoja v4 que el manifiesto publicado debe sembrar, comprobadas sin base de datos.
/// </summary>
public sealed class QaManifestPreconditionTests
{
    private static readonly QaClock Clock = new(new DateTimeOffset(2026, 11, 8, 15, 0, 0, TimeSpan.Zero));

    private static QaManifest Manifest() => QaManifestLoader.LoadFile(QaTestManifests.PublishedPath());

    private static IReadOnlyList<QaSeed> SeedsOf(string cp) => Manifest().FindCase(cp)!.Seeds;

    [Fact]
    public void PublishedManifest_IsVersion12FromSheetV4()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(QaTestManifests.PublishedPath()));

        document.RootElement.GetProperty("manifestVersion").GetString().Should().Be("1.2");
        document.RootElement.GetProperty("source").GetString().Should().StartWith("Casos_de_Prueba-PY20261049_v4.xlsx");
    }

    [Fact]
    public void Cp049_PanelFiveLinkedFourDaysAgoWithoutRecords_OthersWithRecordsToday()
    {
        var seeds = SeedsOf("CP049");

        var panel5Link = seeds.OfType<QaLinkSeed>().Single(link => link.Patient == "paciente.panel5@ejemplo.com");
        panel5Link.Since.Should().Be(QaOffset.DaysAgo(4));
        seeds.Where(seed => seed is QaMealSeed or QaSymptomSeed)
            .Should().NotContain(seed => seed.ReferencedEmails().Contains("paciente.panel5@ejemplo.com"));

        foreach (var patient in new[] { 1, 2, 3, 4, 6 }.Select(number => $"paciente.panel{number}@ejemplo.com"))
        {
            seeds.OfType<QaMealSeed>().Should().Contain(meal => meal.Patient == patient && meal.Ago.Today, patient);
        }
    }

    [Fact]
    public void Cp059_RecordsOnlyAfterSeptemberFifteenth()
    {
        var seeds = SeedsOf("CP059");
        var limit = QaClock.AtLimaTime(new DateOnly(2026, 9, 15), TimeOnly.MaxValue);

        var moments = seeds.OfType<QaMealSeed>().Select(meal => Clock.Resolve(meal.Ago))
            .Concat(seeds.OfType<QaSymptomSeed>().Select(symptom => Clock.Resolve(symptom.Ago)))
            .ToList();

        seeds.OfType<QaMealSeed>().Should().NotBeEmpty();
        seeds.OfType<QaSymptomSeed>().Should().NotBeEmpty();
        moments.Should().OnlyContain(moment => moment > limit && moment <= Clock.Anchor);
        seeds.OfType<QaLinkSeed>().Single().Since.Should().Be(new QaOffset(null, null, false, new DateOnly(2026, 9, 15)));
    }

    [Fact]
    public void Cp036_RecommendationProposesSubstitutesForTheReducedFoods()
    {
        var recommendation = SeedsOf("CP036").OfType<QaRecommendationSeed>().Single();

        recommendation.Substitutes.Should().BeEquivalentTo(new[]
        {
            new QaSubstitute("Cebolla cocida", "Zanahoria cocida"),
            new QaSubstitute("Pan de trigo blanco", "Arroz blanco cocido")
        });
    }

    [Fact]
    public void Cp040_FirstRecommendationCarriesRecentHistory()
    {
        var recommendations = SeedsOf("CP040").OfType<QaRecommendationSeed>().ToList();

        recommendations.Count(recommendation => recommendation.WithRecentHistory).Should().Be(1);
        recommendations[0].WithRecentHistory.Should().BeTrue();
        recommendations[0].Reduce.Should().StartWith("Cebolla cocida");
    }

    [Fact]
    public void Cp068Cp069Cp089_GlossaryFactsAreSeedData()
    {
        SeedsOf("CP068").OfType<QaGlossarySeed>().Should().BeEquivalentTo(new[]
        {
            new QaGlossarySeed("FODMAP", true, "nutritional"),
            new QaGlossarySeed("IBS-SSS", true, "clinical_ibs"),
            new QaGlossarySeed("Línea base", true, "system")
        });
        SeedsOf("CP069").OfType<QaGlossarySeed>().Should().BeEquivalentTo(new[]
        {
            new QaGlossarySeed("Disacáridos", true, null),
            new QaGlossarySeed("disacaridasas", false, null),
            new QaGlossarySeed("xyzqw", false, null)
        });
        SeedsOf("CP089").OfType<QaGlossarySeed>().Should().ContainSingle()
            .Which.Should().Be(new QaGlossarySeed("disacaridasas", false, null));
    }

    [Fact]
    public void Cp065_ProfileUsesTheSubtypeOfTheCase()
    {
        SeedsOf("CP065").OfType<QaProfileSeed>().Single().Subtype.Should().Be("SII-C");
    }

    [Fact]
    public void SupportingHistory_Cp042And077_StayInsideTheWindowForAWeek()
    {
        foreach (var cp in new[] { "CP042", "CP077" })
        {
            var recommendation = SeedsOf(cp).OfType<QaRecommendationSeed>().Single();

            var days = QaRecommendationSeedApplier.SupportingHistoryMoments(recommendation, Clock);

            days.Should().HaveCount(4, cp);
            days.Should().OnlyContain(moment => moment >= Clock.Anchor.AddDays(-4) && moment < Clock.Anchor, cp);
            days.Should().OnlyHaveUniqueItems(cp);
        }
    }

    [Fact]
    public void Parse_GlossarySeedWithUnknownCategory_IsRejected()
    {
        var json = QaTestManifests.Minimal(seeds: """[{"t":"glossary","term":"FODMAP","category":"mystery"}]""");

        var act = () => QaManifestLoader.Parse(json);

        act.Should().Throw<QaManifestException>().Which.Errors.Should().ContainSingle(error => error.Contains("mystery"));
    }

    [Fact]
    public void FindMissing_SubstituteFoodNotInCatalog_IsListed()
    {
        var manifest = QaManifestLoader.Parse(QaTestManifests.Minimal(seeds: """
            [{"t":"rec","patient":"paciente.qa@ejemplo.com","title":"Con sustituto","status":"pendingReview",
              "reduce":["Causa rellena"],"substitutes":[{"food":"Causa rellena","by":"Tomate"}]}]
            """));
        var catalog = new QaCatalog(
            new Dictionary<string, Guid> { ["Causa rellena"] = Guid.NewGuid() },
            new Dictionary<string, Guid>());

        catalog.FindMissing(manifest).Should().Equal("Alimento sin catálogo: «Tomate».");
    }

    [Fact]
    public void OwnerOf_GlossarySeed_IsNobody()
    {
        QaSeedOwnership.OwnerOf(new QaGlossarySeed("FODMAP", true, "nutritional")).Should().BeNull();
    }
}
