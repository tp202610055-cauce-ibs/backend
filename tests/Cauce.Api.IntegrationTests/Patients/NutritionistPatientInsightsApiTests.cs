using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Patients;

/// <summary>
/// Pruebas de integración de las vistas del nutricionista sobre sus pacientes: panel de triaje con
/// priorización (US18) y métricas de evolución (US21), incluyendo el 403 por paciente no asignado.
/// </summary>
[Trait("Category", "Integration")]
public sealed class NutritionistPatientInsightsApiTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public NutritionistPatientInsightsApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
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
    public async Task Evolution_AssignedPatient_ReturnsMetrics()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedAssessmentAsync(patient.Id, AssessmentType.Baseline, 0, 80, DateTime.UtcNow.AddDays(-28)); // total 400
        await SeedAssessmentAsync(patient.Id, AssessmentType.Periodic, 1, 40, DateTime.UtcNow.AddDays(-1));  // total 200
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email)
            .GetAsync($"/api/v1/nutritionists/me/patients/{patient.Id}/evolution");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("baselineScore").GetInt32().Should().Be(400);
        body.GetProperty("latestScore").GetInt32().Should().Be(200);
        body.GetProperty("significantClinicalResponse").GetBoolean().Should().BeTrue();
        body.GetProperty("ibsSssTimeline").GetArrayLength().Should().Be(2);
    }

    [SkippableFact]
    public async Task Evolution_NotAssignedPatient_Returns403()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email)
            .GetAsync($"/api/v1/nutritionists/me/patients/{patient.Id}/evolution");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task Triage_OrdersSeverePatientFirst()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var mild = await SeedPatientAsync();
        await SeedPatientProfileAsync(mild.Id);
        await SeedAssessmentAsync(mild.Id, AssessmentType.Baseline, 0, 20, DateTime.UtcNow.AddDays(-2)); // total 100 (mild)
        await AssignAsync(nutritionist.Id, mild.Id);

        var severe = await SeedPatientAsync();
        await SeedPatientProfileAsync(severe.Id);
        await SeedAssessmentAsync(severe.Id, AssessmentType.Baseline, 0, 90, DateTime.UtcNow.AddDays(-2)); // total 450 (severe)
        await AssignAsync(nutritionist.Id, severe.Id);

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email)
            .GetAsync("/api/v1/nutritionists/me/patients");

        response.EnsureSuccessStatusCode();
        var patients = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        patients.Should().HaveCount(2);
        patients[0].GetProperty("patientUserId").GetGuid().Should().Be(severe.Id);
        patients[0].GetProperty("priorityLevel").GetString().Should().Be("High");
        patients[0].GetProperty("latestIbsSssScore").GetInt32().Should().Be(450);
    }
}
