using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración de la advertencia de alérgenos al crear y al actualizar un alimento
/// personalizado (US10 CA03): 409 con el detalle cuando un ingrediente coincide con una alergia
/// declarada sin confirmar, y guardado con acuse cuando el paciente confirma.
///
/// <para>Van por HTTP y no contra el handler suelto: así atraviesan el pipeline real de MediatR
/// (validación, idempotencia, auditoría, logging), que es lo único que demuestra que el behavior de
/// auditoría engancha de verdad y que el 409 sale con su envelope.</para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class CustomFoodAllergenApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public CustomFoodAllergenApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task<Guid> SeedLactoseFoodAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var food = FoodItem.SeedEntry(
            Guid.NewGuid(), $"Leche entera de vaca {Guid.NewGuid():N}", "lacteos", 61m, 3m, 5m, 3m, 0m, FodmapLevel.High, "lactose", true, DateTime.UtcNow);
        db.FoodItems.Add(food);
        await db.SaveChangesAsync();
        return food.Id;
    }

    private async Task DeclareLactoseAllergyAsync(Guid patientId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var allergyId = await db.Allergies.AsNoTracking().Where(a => a.Name == "Lactosa").Select(a => a.Id).FirstAsync();
        db.Set<PatientAllergy>().Add(PatientAllergy.Declare(Guid.NewGuid(), patientId, allergyId, AllergySeverity.Moderate, null, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    private static object CustomFoodBody(Guid foodId, bool confirmed) => new
    {
        name = $"Preparado-{Guid.NewGuid():N}",
        portionSizeGrams = 200m,
        ingredients = new[] { new { foodId, proportionGrams = 100m } },
        confirmedAllergens = confirmed
    };

    [SkippableFact]
    public async Task Create_WithAllergenIngredientUnconfirmed_Returns409WithDetail()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await DeclareLactoseAllergyAsync(patient.Id);
        var foodId = await SeedLactoseFoodAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody(foodId, confirmed: false));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detected").GetBoolean().Should().BeTrue();
        var allergens = body.GetProperty("allergens").EnumerateArray().ToList();
        allergens.Should().ContainSingle();
        allergens[0].GetProperty("allergenName").GetString().Should().Be("Lactosa");
        allergens[0].GetProperty("ingredientName").GetString().Should().Contain("Leche");
    }

    [SkippableFact]
    public async Task Create_WithAllergenIngredientConfirmed_CreatesAndAudits()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await DeclareLactoseAllergyAsync(patient.Id);
        var foodId = await SeedLactoseFoodAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody(foodId, confirmed: true));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var audit = (await AuditLogsAsync(nameof(CustomFood))).Should().ContainSingle().Subject;
        audit.AdditionalContext.Should().NotBeNull();
        audit.AdditionalContext!.Should().Contain("acknowledged_allergens");
    }

    [SkippableFact]
    public async Task Create_WithoutAllergies_CreatesNormally()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var foodId = await SeedLactoseFoodAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody(foodId, confirmed: false));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [SkippableFact]
    public async Task Update_AddingAllergenIngredientUnconfirmed_Returns409WithDetail()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var harmlessFoodId = await SeedHarmlessFoodAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);

        // Se crea sin alérgenos y recién después se declara la alergia, que es el escenario que la
        // revalidación al crear no podía cubrir.
        var created = await client.PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody(harmlessFoodId, confirmed: false));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var customFoodId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("customFoodId").GetGuid();

        await DeclareLactoseAllergyAsync(patient.Id);
        var lactoseFoodId = await SeedLactoseFoodAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/custom-foods/{customFoodId}",
            UpdateBody(lactoseFoodId, confirmed: false));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("unconfirmed_allergens");
        body.GetProperty("detected").GetBoolean().Should().BeTrue();
        var allergens = body.GetProperty("allergens").EnumerateArray().ToList();
        allergens.Should().ContainSingle();
        allergens[0].GetProperty("allergenName").GetString().Should().Be("Lactosa");
    }

    [SkippableFact]
    public async Task Update_AddingAllergenIngredientConfirmed_SavesAndAudits()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var harmlessFoodId = await SeedHarmlessFoodAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);

        var created = await client.PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody(harmlessFoodId, confirmed: false));
        var customFoodId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("customFoodId").GetGuid();

        await DeclareLactoseAllergyAsync(patient.Id);
        var lactoseFoodId = await SeedLactoseFoodAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/custom-foods/{customFoodId}",
            UpdateBody(lactoseFoodId, confirmed: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // El acuse queda en la bitácora de la actualización, no solo en la de la creación.
        var audits = await AuditLogsAsync(nameof(CustomFood));
        var update = audits.Should().ContainSingle(log => log.ActionType == AuditActionType.Update).Subject;
        update.AdditionalContext.Should().NotBeNull();
        update.AdditionalContext!.Should().Contain("acknowledged_allergens");
    }

    [SkippableFact]
    public async Task Update_WithoutAllergenIngredients_SavesWithoutAcknowledgement()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await DeclareLactoseAllergyAsync(patient.Id);
        var harmlessFoodId = await SeedHarmlessFoodAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);

        var created = await client.PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody(harmlessFoodId, confirmed: false));
        var customFoodId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("customFoodId").GetGuid();

        var otherHarmlessId = await SeedHarmlessFoodAsync();
        var response = await client.PutAsJsonAsync(
            $"/api/v1/custom-foods/{customFoodId}",
            UpdateBody(otherHarmlessId, confirmed: false));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var audits = await AuditLogsAsync(nameof(CustomFood));
        audits.Single(log => log.ActionType == AuditActionType.Update).AdditionalContext.Should().BeNull();
    }

    private async Task<Guid> SeedHarmlessFoodAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var food = FoodItem.SeedEntry(
            Guid.NewGuid(), $"Arroz blanco {Guid.NewGuid():N}", "cereales", 130m, 2m, 28m, 0m, 0m, FodmapLevel.Low, null, true, DateTime.UtcNow);
        db.FoodItems.Add(food);
        await db.SaveChangesAsync();
        return food.Id;
    }

    private static object UpdateBody(Guid foodId, bool confirmed) => new
    {
        name = $"Preparado-{Guid.NewGuid():N}",
        portionSizeGrams = 250m,
        ingredients = new[] { new { foodId, proportionGrams = 120m } },
        confirmedAllergens = confirmed
    };
}
