using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Patients;

/// <summary>
/// Pruebas de integración de las vistas del propio paciente: sugerencias de alimentos (US09 CA03) y
/// perfil agregado "mi perfil" (US28).
/// </summary>
[Trait("Category", "Integration")]
public sealed class PatientSelfInsightsApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public PatientSelfInsightsApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task<Guid> SeedMealAsync(Guid patientId, DateTime consumedAt)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var foodId = await db.FoodItems.AsNoTracking().OrderBy(f => f.Name).Select(f => f.Id).FirstAsync();
        var meal = Meal.Register(
            Guid.NewGuid(), Guid.NewGuid(), patientId, MealTime.Lunch, consumedAt, consumedAt,
            new[] { new MealItemInput(foodId, null, 100m, MeasurementUnit.Grams) }, DateTime.UtcNow);
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        return foodId;
    }

    private async Task SeedAssessmentAsync(Guid patientId, AssessmentType type, int cycle, int dimension, DateTime completedAt)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var assessment = IbsSssAssessment.Submit(
            Guid.NewGuid(), patientId, type, cycle, dimension, dimension, dimension, dimension, dimension, completedAt);
        db.IbsSssAssessments.Add(assessment);
        await db.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task FoodSuggestions_ReturnsFrequentRecentAndCatalogLists()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var recentFoodId = await SeedMealAsync(patient.Id, DateTime.UtcNow.AddHours(-12));

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync("/api/v1/foods/suggestions");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var recent = body.GetProperty("recentLast24Hours").EnumerateArray().Select(f => f.GetProperty("foodId").GetGuid()).ToList();
        recent.Should().Contain(recentFoodId);
        body.GetProperty("frequentLast30Days").GetArrayLength().Should().BeGreaterThan(0);
        body.GetProperty("catalogSuggestions").GetArrayLength().Should().BeGreaterThan(0);
    }

    [SkippableFact]
    public async Task MyProfileSummary_ReturnsAggregatedMetricsAndMaskedEmail()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedAssessmentAsync(patient.Id, AssessmentType.Baseline, 0, 80, DateTime.UtcNow.AddDays(-28)); // 400
        await SeedAssessmentAsync(patient.Id, AssessmentType.Periodic, 1, 30, DateTime.UtcNow.AddDays(-1));  // 150
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync("/api/v1/patients/me/summary");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("ibsSssBaseline").GetInt32().Should().Be(400);
        body.GetProperty("ibsSssLatest").GetInt32().Should().Be(150);
        body.GetProperty("cumulativeChange").GetInt32().Should().Be(-250);
        body.GetProperty("significantClinicalResponse").GetBoolean().Should().BeTrue();
        body.GetProperty("assignedNutritionist").ValueKind.Should().NotBe(JsonValueKind.Null);
        body.GetProperty("patient").GetProperty("maskedEmail").GetString().Should().Contain("***@cauce.local");
    }

    [SkippableFact]
    public async Task MyProfileSummary_CarriesThePatientCode()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync("/api/v1/patients/me/summary");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // HU0028 / CP070: la pantalla de perfil muestra el código con el que el paciente figura en el
        // estudio. Se compara contra el valor persistido y no solo contra el formato, para que la
        // prueba falle si alguna vez se devolviera el código de otra cuenta.
        var code = body.GetProperty("patient").GetProperty("patientCode").GetString();
        code.Should().NotBeNullOrWhiteSpace();
        PatientCode.TryParse(code, out _).Should().BeTrue($"'{code}' debe respetar el formato PAC-0000");

        var (scope, db) = CreateDbScope();
        using var disposableScope = scope;
        var stored = await db.Users.AsNoTracking().Where(u => u.Id == patient.Id).Select(u => u.PatientCode).FirstAsync();
        code.Should().Be(stored);
    }

    [SkippableFact]
    public async Task MyProfileSummary_CarriesTheDeclaredAllergiesInTheSameShapeAsTheAllergiesEndpoint()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var allergyId = await FirstActiveAllergyIdAsync();

        var client = PatientClient(patient.KeycloakId, patient.Email);
        var declared = await client.PostAsJsonAsync(
            "/api/v1/patients/allergies",
            new { allergyId, severity = "Moderate", notes = "Declarada para la prueba de perfil." });
        declared.EnsureSuccessStatusCode();

        var summary = await client.GetAsync("/api/v1/patients/me/summary");
        summary.EnsureSuccessStatusCode();
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        var fromSummary = summaryBody.GetProperty("clinical").GetProperty("allergies").EnumerateArray().ToList();

        fromSummary.Should().ContainSingle();
        fromSummary[0].GetProperty("allergyId").GetGuid().Should().Be(allergyId);
        fromSummary[0].GetProperty("allergyName").GetString().Should().NotBeNullOrWhiteSpace();
        fromSummary[0].GetProperty("severity").GetString().Should().Be("Moderate");

        // La forma tiene que ser la misma que la del endpoint dedicado: si divergieran, el cliente
        // necesitaría dos modelos para el mismo dato.
        var dedicated = await client.GetAsync("/api/v1/patients/allergies");
        dedicated.EnsureSuccessStatusCode();
        var fromEndpoint = (await dedicated.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        fromEndpoint.Should().ContainSingle();
        fromSummary[0].GetRawText().Should().Be(fromEndpoint[0].GetRawText());
    }

    [SkippableFact]
    public async Task MyProfileSummary_WithoutDeclaredAllergies_ReturnsAnEmptyListAndNotNull()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email).GetAsync("/api/v1/patients/me/summary");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var allergies = body.GetProperty("clinical").GetProperty("allergies");

        allergies.ValueKind.Should().Be(JsonValueKind.Array, "una lista vacía es más fácil de consumir que un null");
        allergies.GetArrayLength().Should().Be(0);
    }
}
