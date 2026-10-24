using Cauce.Infrastructure.Persistence.Seeders;
using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Qa.Manifest;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Qa;

public sealed class QaManifestLoaderTests
{
    [Fact]
    public void LoadFile_PublishedManifest_LoadsAllAccountsAndCases()
    {
        var manifest = QaManifestLoader.LoadFile(QaTestManifests.PublishedPath());

        manifest.Cases.Should().HaveCount(90);
        manifest.SeededAccounts.Should().HaveCount(105);
        manifest.MustNotExistAccounts.Should().HaveCount(11);
    }

    [Fact]
    public void FindMissing_PublishedManifestAgainstInitialCatalog_FindsNothingMissing()
    {
        var manifest = QaManifestLoader.LoadFile(QaTestManifests.PublishedPath());
        var catalog = new QaCatalog(
            FoodItemsSeeder.CatalogFoods.ToDictionary(food => food.Name, _ => Guid.NewGuid()),
            AllergiesSeeder.CatalogNames.ToDictionary(name => name, _ => Guid.NewGuid()));

        catalog.FindMissing(manifest).Should().BeEmpty();
    }

    [Fact]
    public void Parse_UnknownVersion_FailsNamingTheVersion()
    {
        var act = () => QaManifestLoader.Parse(QaTestManifests.Minimal(version: "9.9"));

        act.Should().Throw<QaManifestException>()
            .Which.Errors.Should().ContainSingle(error => error.Contains("9.9"));
    }

    [Fact]
    public void Parse_UnknownSeedType_FailsNamingTheCaseAndType()
    {
        var json = QaTestManifests.Minimal(seeds: """[{"t":"teleport","patient":"paciente.qa@ejemplo.com"}]""");

        var act = () => QaManifestLoader.Parse(json);

        act.Should().Throw<QaManifestException>()
            .Which.Errors.Should().ContainSingle(error => error.Contains("CP900") && error.Contains("teleport"));
    }

    [Fact]
    public void Parse_SeedReferencesUnknownAccountAndWrongRole_ReportsBoth()
    {
        var json = QaTestManifests.Minimal(seeds: """
            [
              {"t":"profile","patient":"nadie@ejemplo.com","complete":false},
              {"t":"link","patient":"paciente.qa@ejemplo.com","nutritionist":"paciente.qa@ejemplo.com"}
            ]
            """);

        var act = () => QaManifestLoader.Parse(json);

        var errors = act.Should().Throw<QaManifestException>().Which.Errors;
        errors.Should().Contain(error => error.Contains("nadie@ejemplo.com"));
        errors.Should().Contain(error => error.Contains("Nutritionist"));
    }

    [Fact]
    public void Parse_OffsetWithTwoUnits_IsRejected()
    {
        var json = QaTestManifests.Minimal(seeds: """
            [{"t":"meal","patient":"paciente.qa@ejemplo.com","food":"Causa rellena","ago":{"days":1,"hours":2}}]
            """);

        var act = () => QaManifestLoader.Parse(json);

        act.Should().Throw<QaManifestException>().Which.Errors.Should().ContainSingle(error => error.Contains("ago"));
    }

    [Fact]
    public void FindMissing_SeveralUnknownNames_ListsEveryOne()
    {
        var manifest = QaManifestLoader.Parse(QaTestManifests.Minimal(seeds: """
            [
              {"t":"profile","patient":"paciente.qa@ejemplo.com","complete":true,"allergies":["Maní"]},
              {"t":"meal","patient":"paciente.qa@ejemplo.com","food":"Ajo","ago":{"today":true}},
              {"t":"rec","patient":"paciente.qa@ejemplo.com","title":"Sin alimentos","status":"pendingReview"},
              {"t":"customFood","patient":"paciente.qa@ejemplo.com","name":"Mi plato","ingredients":["Tomate","Causa rellena"]}
            ]
            """));
        var catalog = new QaCatalog(
            new Dictionary<string, Guid> { ["Causa rellena"] = Guid.NewGuid() },
            new Dictionary<string, Guid> { ["Lactosa"] = Guid.NewGuid() });

        var missing = catalog.FindMissing(manifest);

        missing.Should().BeEquivalentTo(
            "Alimento sin catálogo: «Ajo».",
            "Alimento sin catálogo: «Cebolla cocida».",
            "Alimento sin catálogo: «Tomate».",
            "Alergia sin catálogo: «Maní».");
    }

    [Fact]
    public void EngineFoods_RecommendationWithoutFoods_ReducesTheDefaultFood()
    {
        var manifest = QaManifestLoader.Parse(QaTestManifests.Minimal(seeds: """
            [{"t":"rec","patient":"paciente.qa@ejemplo.com","title":"Sin alimentos","status":"approvedAsGenerated"}]
            """));
        var recommendation = manifest.Cases[0].Seeds.OfType<QaRecommendationSeed>().Single();

        QaCatalog.EngineFoods(recommendation).Should().Equal((QaCatalog.DefaultReduceFood, QaItemAction.Reduce));
    }

    [Fact]
    public void Locate_WithoutExplicitPath_UsesTheSinglePublishedManifest()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory.FullName, "qa"));
            var expected = Path.Combine(directory.FullName, "qa", "qa-manifest-a.json");
            File.WriteAllText(expected, "{}");

            QaManifestLocator.Resolve(null, directory.FullName).Should().Be(expected);

            File.WriteAllText(Path.Combine(directory.FullName, "qa", "qa-manifest-b.json"), "{}");
            var act = () => QaManifestLocator.Resolve(null, directory.FullName);
            act.Should().Throw<QaManifestException>();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
