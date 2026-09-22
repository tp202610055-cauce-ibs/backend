using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración del nivel FODMAP agregado de una comida. Comprueban que la lectura
/// devuelva lo mismo que el alta: hasta este bloque, <c>POST /meals</c> respondía el nivel calculado
/// y <c>GET /meals</c> lo devolvía siempre en <c>null</c>, de modo que el distintivo FODMAP del
/// diario de la app nunca llegaba a mostrarse.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MealFodmapApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public MealFodmapApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task<Guid> SeedFoodAsync(FodmapLevel level)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var food = FoodItem.SeedEntry(
            Guid.NewGuid(), $"Alimento {level} {Guid.NewGuid():N}", "verduras",
            50m, 1m, 10m, 0m, 1m, level, null, true, DateTime.UtcNow);
        db.FoodItems.Add(food);
        await db.SaveChangesAsync();
        return food.Id;
    }

    private static object MealBody(params Guid[] foodIds)
    {
        var at = DateTime.UtcNow.AddHours(-1);
        return new
        {
            mealTime = "Lunch",
            consumedAt = at,
            clientCreatedAt = at,
            items = foodIds.Select(id => new { foodId = id, quantity = 150m, unit = "Grams" }).ToArray()
        };
    }

    private async Task<JsonElement> GetHistoryAsync(HttpClient client)
    {
        var from = DateTime.UtcNow.AddDays(-2).ToString("O");
        var to = DateTime.UtcNow.AddDays(1).ToString("O");
        var response = await client.GetAsync($"/api/v1/meals?from={from}&to={to}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [SkippableTheory]
    [InlineData(FodmapLevel.Low, "Low")]
    [InlineData(FodmapLevel.Moderate, "Moderate")]
    [InlineData(FodmapLevel.High, "High")]
    public async Task GetHistory_ReturnsTheSameAggregatedLevelAsThePost(FodmapLevel level, string expected)
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var foodId = await SeedFoodAsync(level);

        var created = await PostWithKey(client, "/api/v1/meals", MealBody(foodId));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var postLevel = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("aggregatedFodmap").GetString();
        postLevel.Should().Be(expected);

        var history = await GetHistoryAsync(client);
        var meals = history.GetProperty("items").EnumerateArray().ToList();
        meals.Should().ContainSingle();
        meals[0].GetProperty("aggregatedFodmap").GetString().Should().Be(postLevel);
    }

    [SkippableFact]
    public async Task GetHistory_MixedLevels_TakesTheHighest()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var low = await SeedFoodAsync(FodmapLevel.Low);
        var high = await SeedFoodAsync(FodmapLevel.High);

        await PostWithKey(client, "/api/v1/meals", MealBody(low, high));

        var history = await GetHistoryAsync(client);
        var meals = history.GetProperty("items").EnumerateArray().ToList();
        meals[0].GetProperty("aggregatedFodmap").GetString().Should().Be("High",
            "la heurística conservadora toma el máximo entre los ítems, igual que en el alta");
    }

    [SkippableFact]
    public async Task UnifiedHistory_AlsoCarriesTheAggregatedLevel()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var foodId = await SeedFoodAsync(FodmapLevel.Moderate);

        await PostWithKey(client, "/api/v1/meals", MealBody(foodId));

        var from = DateTime.UtcNow.AddDays(-2).ToString("O");
        var to = DateTime.UtcNow.AddDays(1).ToString("O");
        var response = await client.GetAsync($"/api/v1/history?from={from}&to={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var mealEvent = events.Single(e => e.GetProperty("eventType").GetString() == "meal");
        mealEvent.GetProperty("meal").GetProperty("aggregatedFodmap").GetString().Should().Be("Moderate");
    }

    [SkippableFact]
    public async Task GetHistory_MealWithoutCatalogItems_ReportsTheConservativeDefault()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var ingredientId = await SeedFoodAsync(FodmapLevel.High);

        var customFood = await client.PostAsJsonAsync("/api/v1/custom-foods", new
        {
            name = $"Preparado-{Guid.NewGuid():N}",
            portionSizeGrams = 200m,
            ingredients = new[] { new { foodId = ingredientId, proportionGrams = 100m } }
        });
        var customFoodId = (await customFood.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("customFoodId").GetGuid();

        var at = DateTime.UtcNow.AddHours(-1);
        var created = await PostWithKey(client, "/api/v1/meals", new
        {
            mealTime = "Dinner",
            consumedAt = at,
            clientCreatedAt = at,
            items = new[] { new { customFoodId, quantity = 1m, unit = "Units" } }
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var postLevel = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("aggregatedFodmap").GetString();

        var history = await GetHistoryAsync(client);
        var meals = history.GetProperty("items").EnumerateArray().ToList();

        // Los alimentos personalizados quedan fuera de la agregación (su nivel se derivaría de sus
        // ingredientes, que es deuda abierta). Lo que esta prueba fija es que la lectura y el alta
        // digan lo mismo, no que el valor sea clínicamente el correcto.
        meals[0].GetProperty("aggregatedFodmap").GetString().Should().Be(postLevel);
    }
}
