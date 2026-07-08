using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Patients;

/// <summary>
/// Pruebas de integración de las vistas del propio paciente: sugerencias de alimentos (US09 CA03) y
/// perfil agregado "mi perfil" (US28).
/// </summary>
[Trait("Category", "Integration")]
public sealed class PatientSelfInsightsApiTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
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
}
