using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración del glosario clínico (US27): listado ordenado, definición según rol, estado
/// del contenido y búsqueda insensible a mayúsculas y a tildes (extensión <c>unaccent</c>).
/// </summary>
[Trait("Category", "Integration")]
public sealed class GlossaryApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public GlossaryApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task SeedGlossaryAsync()
    {
        var (scope, _) = CreateDbScope();
        using var _scope = scope;
        await scope.ServiceProvider.GetRequiredService<GlossaryTermsSeeder>().SeedAsync();
    }

    [SkippableFact]
    public async Task List_AsPatient_ReturnsPatientDefinitionsOrderedWithDraftStatus()
    {
        SkipIfUnavailable();
        await SeedGlossaryAsync();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync("/api/v1/glossary");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("contentStatus").GetString().Should().Be("draft-pending-clinical-review");

        var terms = body.GetProperty("terms").EnumerateArray().ToList();
        terms.Should().NotBeEmpty();
        var names = terms.Select(t => t.GetProperty("term").GetString()!).ToList();
        names.Should().BeInAscendingOrder();

        var fodmap = terms.Single(t => t.GetProperty("term").GetString() == "FODMAP");
        fodmap.GetProperty("definition").GetString().Should().Contain("azúcares");
    }

    [SkippableFact]
    public async Task List_AsNutritionist_ReturnsTechnicalDefinition()
    {
        SkipIfUnavailable();
        await SeedGlossaryAsync();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email).GetAsync("/api/v1/glossary");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fodmap = body.GetProperty("terms").EnumerateArray().Single(t => t.GetProperty("term").GetString() == "FODMAP");
        fodmap.GetProperty("definition").GetString().Should().Contain("Oligosacáridos");
    }

    [SkippableFact]
    public async Task Search_WithoutAccent_MatchesAccentedTermViaUnaccent()
    {
        SkipIfUnavailable();
        await SeedGlossaryAsync();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync("/api/v1/glossary/search?q=distension");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var names = body.GetProperty("terms").EnumerateArray().Select(t => t.GetProperty("term").GetString()).ToList();
        names.Should().Contain("Distensión abdominal");
    }
}
