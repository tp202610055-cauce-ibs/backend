using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración de la búsqueda del catálogo de alimentos. Se ejecutan contra PostgreSQL
/// real y no contra un doble, porque lo que se verifica es la traducción a <c>unaccent(...) ILIKE</c>
/// y el comportamiento de la extensión: con un repositorio simulado la consulta nunca se traduce y la
/// prueba no diría nada.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FoodSearchApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public FoodSearchApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task<string> SeedAccentedFoodAsync(string name)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var unique = $"{name} {Guid.NewGuid():N}";
        db.FoodItems.Add(FoodItem.SeedEntry(
            Guid.NewGuid(), unique, "frutas", 89m, 1.1m, 22.8m, 0.3m, 2.6m, FodmapLevel.Low, null, true, DateTime.UtcNow));
        await db.SaveChangesAsync();
        return unique;
    }

    private async Task<List<JsonElement>> SearchAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/v1/foods/search?q={Uri.EscapeDataString(query)}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.EnumerateArray().ToList();
    }

    [SkippableFact]
    public async Task Search_WithoutAccents_FindsAccentedNames()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var name = await SeedAccentedFoodAsync("Plátano de la isla");

        // El teclado del celular no pone tildes por defecto: "platano" tiene que encontrar "plátano".
        var results = await SearchAsync(client, "platano");

        results.Select(r => r.GetProperty("name").GetString()).Should().Contain(name);
    }

    [SkippableFact]
    public async Task Search_WithAccents_StillFindsAccentedNames()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var name = await SeedAccentedFoodAsync("Maíz morado");

        var results = await SearchAsync(client, "maíz");

        results.Select(r => r.GetProperty("name").GetString()).Should().Contain(name);
    }

    [SkippableFact]
    public async Task Search_AccentedQueryAgainstPlainName_AlsoMatches()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var name = await SeedAccentedFoodAsync("Camote sancochado");

        // La normalización se aplica a los dos lados de la comparación, no solo a la columna.
        var results = await SearchAsync(client, "camoté");

        results.Select(r => r.GetProperty("name").GetString()).Should().Contain(name);
    }

    [SkippableFact]
    public async Task Search_IsCaseInsensitive()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var name = await SeedAccentedFoodAsync("Níspero");

        var results = await SearchAsync(client, "NISPERO");

        results.Select(r => r.GetProperty("name").GetString()).Should().Contain(name);
    }

    [SkippableFact]
    public async Task Search_NonMatchingQuery_ReturnsNothing()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);
        await SeedAccentedFoodAsync("Plátano de la isla");

        var results = await SearchAsync(client, $"inexistente-{Guid.NewGuid():N}");

        results.Should().BeEmpty();
    }
}
