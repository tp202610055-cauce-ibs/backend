using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de <c>GET /foods/{foodId}/allergen-check</c>: el aviso de alérgeno al elegir un ingrediente,
/// con la misma regla que el chequeo al guardar el alimento personalizado.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FoodAllergenCheckApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public FoodAllergenCheckApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task<Guid> SeedFoodAsync(string name, string category, string fodmapTags, bool active = true)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var food = FoodItem.SeedEntry(
            Guid.NewGuid(), $"{name} {Guid.NewGuid():N}", category, 61m, 3m, 5m, 3m, 0m, FodmapLevel.High, fodmapTags, true, DateTime.UtcNow);
        if (!active)
        {
            food.Deactivate(DateTime.UtcNow);
        }

        db.FoodItems.Add(food);
        await db.SaveChangesAsync();
        return food.Id;
    }

    private Task<Guid> SeedLactoseFoodAsync() => SeedFoodAsync("Queso fresco", "lacteos", "lactose");

    private async Task DeclareLactoseAllergyAsync(Guid patientId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var allergyId = await db.Allergies.AsNoTracking().Where(a => a.Name == "Lactosa").Select(a => a.Id).FirstAsync();
        db.Set<PatientAllergy>().Add(PatientAllergy.Declare(Guid.NewGuid(), patientId, allergyId, AllergySeverity.Moderate, null, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    private static string Url(Guid foodId) => $"/api/v1/foods/{foodId}/allergen-check";

    [SkippableFact]
    public async Task Check_FoodWithADeclaredAllergen_ReturnsTheAllergenName()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await DeclareLactoseAllergyAsync(patient.Id);
        var foodId = await SeedLactoseFoodAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync(Url(foodId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("foodId").GetGuid().Should().Be(foodId);
        body.GetProperty("hasAllergenConflict").GetBoolean().Should().BeTrue();
        body.GetProperty("allergens").EnumerateArray().Select(a => a.GetString()).Should().Equal("Lactosa");
    }

    [SkippableFact]
    public async Task Check_PatientWithoutAllergies_ReturnsNoConflict()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var foodId = await SeedLactoseFoodAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync(Url(foodId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("hasAllergenConflict").GetBoolean().Should().BeFalse();
        body.GetProperty("allergens").GetArrayLength().Should().Be(0);
    }

    [SkippableTheory]
    [InlineData("Queso fresco", "lacteos", "lactose")]
    [InlineData("Arroz blanco cocido", "cereales", "")]
    public async Task Check_AgreesWithTheSaveCheck(string name, string category, string tags)
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await DeclareLactoseAllergyAsync(patient.Id);
        var foodId = await SeedFoodAsync(name, category, tags);
        var client = PatientClient(patient.KeycloakId, patient.Email);

        var check = await (await client.GetAsync(Url(foodId))).Content.ReadFromJsonAsync<JsonElement>();
        var save = await client.PostAsJsonAsync("/api/v1/custom-foods", new
        {
            name = $"Preparado-{Guid.NewGuid():N}",
            portionSizeGrams = 200m,
            ingredients = new[] { new { foodId, proportionGrams = 100m } },
            confirmedAllergens = false
        });

        var saveDetected = save.StatusCode == HttpStatusCode.Conflict;
        check.GetProperty("hasAllergenConflict").GetBoolean().Should().Be(saveDetected);
    }

    [SkippableFact]
    public async Task Check_EmptyFoodId_Returns400()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync(Url(Guid.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [SkippableFact]
    public async Task Check_UnknownFood_Returns404()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync(Url(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("food_item_not_found");
    }

    [SkippableFact]
    public async Task Check_InactiveFood_Returns404()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var foodId = await SeedFoodAsync("Queso retirado", "lacteos", "lactose", active: false);

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync(Url(foodId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task Check_AsNutritionist_Returns403()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        var foodId = await SeedLactoseFoodAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email).GetAsync(Url(foodId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task Check_WithoutToken_Returns401()
    {
        SkipIfUnavailable();
        var foodId = await SeedLactoseFoodAsync();

        var response = await Factory.CreateClient().GetAsync(Url(foodId));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
