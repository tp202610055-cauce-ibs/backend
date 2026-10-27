using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Patients;

/// <summary>
/// Pruebas de integración de las lecturas clínicas del nutricionista sobre un paciente asignado:
/// diario, recomendaciones y ciclo IBS-SSS. Exigen asignación activa y quedan auditadas.
/// </summary>
[Trait("Category", "Integration")]
public sealed class NutritionistPatientRecordsApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public NutritionistPatientRecordsApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    // ----- Diario -----

    [SkippableFact]
    public async Task History_AssignedPatient_ReturnsMealsSymptomsAndNotesAndAudits()
    {
        SkipIfUnavailable();
        var (patient, nutritionist) = await SeedAssignedPairAsync();
        var consumedAt = DateTime.UtcNow.AddHours(-2);
        await SeedMealWithNoteAsync(patient.Id, consumedAt, "Tuve un día de mucho estrés laboral");
        await SeedSymptomAsync(patient.Id, consumedAt.AddHours(1));

        var response = await NutritionistClient(nutritionist.KeycloakId).GetAsync(HistoryUrl(patient.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        events.Select(e => e.GetProperty("eventType").GetString()).Should().BeEquivalentTo(["meal", "symptom", "clinical_note"]);
        events.Single(e => e.GetProperty("eventType").GetString() == "clinical_note")
            .GetProperty("note").GetProperty("content").GetString().Should().Be("Tuve un día de mucho estrés laboral");

        var audit = (await AuditLogsAsync(nameof(User), patient.Id, AuditActionType.ViewPatientRecord)).Should().ContainSingle().Subject;
        ContextValue(audit, "view").Should().Be("history");
    }

    [SkippableFact]
    public async Task History_UnassignedPatient_Returns403WithoutAudit()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId).GetAsync(HistoryUrl(patient.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AuditLogsAsync(nameof(User), patient.Id, AuditActionType.ViewPatientRecord)).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task History_AsPatient_Returns403()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId).GetAsync(HistoryUrl(patient.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task History_WithoutTimeZone_Returns400()
    {
        SkipIfUnavailable();
        var (patient, nutritionist) = await SeedAssignedPairAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId)
            .GetAsync($"/api/v1/nutritionists/me/patients/{patient.Id}/history?from=2026-10-01T00:00:00&to=2026-10-02T00:00:00");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ----- Recomendaciones -----

    [SkippableFact]
    public async Task Recommendations_AssignedPatient_ListsActiveAndArchivedAndAudits()
    {
        SkipIfUnavailable();
        var (patient, nutritionist) = await SeedAssignedPairAsync();
        var client = NutritionistClient(nutritionist.KeycloakId);
        var kept = await CreateManualAsync(client, patient.Id, "Hidratación entre comidas");
        var archived = await CreateManualAsync(client, patient.Id, "Reducción gradual de lácteos");
        (await client.PostAsJsonAsync($"/api/v1/recommendations/{archived}/archive", new { reason = "ObjectiveMet" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.GetAsync($"/api/v1/nutritionists/me/patients/{patient.Id}/recommendations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalCount").GetInt32().Should().Be(2);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        var active = items.Single(i => i.GetProperty("recommendationId").GetGuid() == kept);
        active.GetProperty("isActive").GetBoolean().Should().BeTrue();
        active.GetProperty("title").GetString().Should().Be("Hidratación entre comidas");
        active.GetProperty("status").GetString().Should().Be("ManualApproved");
        var gone = items.Single(i => i.GetProperty("recommendationId").GetGuid() == archived);
        gone.GetProperty("isActive").GetBoolean().Should().BeFalse();
        gone.GetProperty("archiveReason").GetString().Should().Be("ObjectiveMet");
        gone.GetProperty("archivedAt").ValueKind.Should().NotBe(JsonValueKind.Null);

        var audit = (await AuditLogsAsync(nameof(User), patient.Id, AuditActionType.ViewPatientRecord)).Should().ContainSingle().Subject;
        ContextValue(audit, "view").Should().Be("recommendations");
    }

    [SkippableFact]
    public async Task Recommendations_EachRowListsItsFoodsWithTheirAction()
    {
        SkipIfUnavailable();
        var (patient, nutritionist) = await SeedAssignedPairAsync();
        var client = NutritionistClient(nutritionist.KeycloakId);
        var manual = await CreateManualAsync(client, patient.Id, "Hidratación entre comidas");
        Guid generatedId;
        List<string> foodNames;
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var modelVersionId = await db.ModelVersions.AsNoTracking().Select(v => v.Id).FirstAsync();
            var foods = await db.FoodItems.AsNoTracking().OrderBy(f => f.Name).Take(3).ToListAsync();
            foodNames = foods.Select(f => f.Name).ToList();
            var now = DateTime.UtcNow.AddHours(-1);
            var generated = Recommendation.Generate(
                patient.Id, modelVersionId, ConfidenceScore.Create(0.80m), ExplanationSource.LlmGenerated, "Explicación.",
                [
                    RecommendationItem.Create(foods[0].Id, ActionType.Reduce, null, null),
                    RecommendationItem.Create(foods[1].Id, ActionType.Substitute, null, foods[2].Id)
                ],
                now, TimeSpan.FromHours(72));
            generated.MarkPendingReview(now);
            generated.Approve(nutritionist.Id, "Nota clínica de prueba.", now);
            db.Recommendations.Add(generated);
            await db.SaveChangesAsync();
            generatedId = generated.Id;
        }

        var body = await (await client.GetAsync($"/api/v1/nutritionists/me/patients/{patient.Id}/recommendations"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var items = body.GetProperty("items").EnumerateArray().ToList();
        var row = items.Single(i => i.GetProperty("recommendationId").GetGuid() == generatedId);
        var foodsInRow = row.GetProperty("foods").EnumerateArray().ToList();
        foodsInRow.Should().HaveCount(2);
        var reduced = foodsInRow.Single(f => f.GetProperty("actionType").GetString() == "Reduce");
        reduced.GetProperty("foodName").GetString().Should().Be(foodNames[0]);
        reduced.GetProperty("substituteFoodName").ValueKind.Should().Be(JsonValueKind.Null);
        var substituted = foodsInRow.Single(f => f.GetProperty("actionType").GetString() == "Substitute");
        substituted.GetProperty("foodName").GetString().Should().Be(foodNames[1]);
        substituted.GetProperty("substituteFoodName").GetString().Should().Be(foodNames[2]);
        var manualRow = items.Single(i => i.GetProperty("recommendationId").GetGuid() == manual);
        manualRow.GetProperty("foods").GetArrayLength().Should().Be(0);
    }

    [SkippableFact]
    public async Task Recommendations_UnassignedPatient_Returns403()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId)
            .GetAsync($"/api/v1/nutritionists/me/patients/{patient.Id}/recommendations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----- Ciclo IBS-SSS -----

    [SkippableFact]
    public async Task IbsSssCycle_WithoutSchedule_ReturnsNone()
    {
        SkipIfUnavailable();
        var (patient, nutritionist) = await SeedAssignedPairAsync();

        var body = await GetCycleAsync(nutritionist.KeycloakId, patient.Id);

        body.GetProperty("status").GetString().Should().Be("None");
        body.GetProperty("dueDate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [SkippableTheory]
    [InlineData(false, "Pending")]
    [InlineData(true, "Missed")]
    public async Task IbsSssCycle_LatestSchedule_ReturnsItsStatusAndAudits(bool missed, string expected)
    {
        SkipIfUnavailable();
        var (patient, nutritionist) = await SeedAssignedPairAsync();
        var dueDate = DateTime.UtcNow.AddDays(missed ? -9 : -2);
        await SeedScheduleAsync(patient.Id, dueDate, missed);

        var body = await GetCycleAsync(nutritionist.KeycloakId, patient.Id);

        body.GetProperty("status").GetString().Should().Be(expected);
        body.GetProperty("dueDate").GetDateTime().Should().BeCloseTo(dueDate, TimeSpan.FromSeconds(1));
        var audit = (await AuditLogsAsync(nameof(User), patient.Id, AuditActionType.ViewPatientRecord)).Should().ContainSingle().Subject;
        ContextValue(audit, "view").Should().Be("ibs_sss_cycle");
    }

    [SkippableFact]
    public async Task IbsSssCycle_UnassignedPatient_Returns403()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId)
            .GetAsync($"/api/v1/nutritionists/me/patients/{patient.Id}/ibs-sss-cycle");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----- Helpers -----

    private static string HistoryUrl(Guid patientId)
    {
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.AddHours(1).ToString("O"));
        return $"/api/v1/nutritionists/me/patients/{patientId}/history?from={from}&to={to}";
    }

    private async Task<((Guid Id, string KeycloakId, string Email) Patient, (Guid Id, string KeycloakId, string Email) Nutritionist)> SeedAssignedPairAsync()
    {
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);
        return (patient, nutritionist);
    }

    private async Task SeedMealWithNoteAsync(Guid patientId, DateTime consumedAt, string note)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var foodId = await db.FoodItems.AsNoTracking().OrderBy(f => f.Name).Select(f => f.Id).FirstAsync();
        var meal = Meal.Register(
            Guid.NewGuid(), Guid.NewGuid(), patientId, MealTime.Lunch, consumedAt, consumedAt,
            new[] { new MealItemInput(foodId, null, 100m, MeasurementUnit.Grams) }, DateTime.UtcNow);
        db.Meals.Add(meal);
        db.ClinicalNotes.Add(ClinicalNote.Attach(Guid.NewGuid(), Guid.NewGuid(), patientId, meal.Id, null, note, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    private async Task SeedSymptomAsync(Guid patientId, DateTime occurredAt)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        db.Symptoms.Add(Symptom.Report(
            Guid.NewGuid(), Guid.NewGuid(), patientId, SymptomType.AbdominalPain, 65, occurredAt, occurredAt, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    private async Task SeedScheduleAsync(Guid patientId, DateTime dueDate, bool missed)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var schedule = IbsSssAssessmentSchedule.Create(Guid.NewGuid(), patientId, dueDate, dueDate.AddDays(-14));
        if (missed)
        {
            schedule.MarkMissed();
        }

        db.IbsSssSchedules.Add(schedule);
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> CreateManualAsync(HttpClient client, Guid patientId, string title)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/recommendations/manual",
            new
            {
                patientId,
                title,
                description = "Recomendación creada por el nutricionista para la prueba.",
                steps = Array.Empty<string>(),
                clinicalNote = "Nota clínica suficiente para la prueba.",
                validUntil = (DateTime?)null
            });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recommendationId").GetGuid();
    }

    private async Task<JsonElement> GetCycleAsync(string nutritionistKeycloakId, Guid patientId)
    {
        var response = await NutritionistClient(nutritionistKeycloakId)
            .GetAsync($"/api/v1/nutritionists/me/patients/{patientId}/ibs-sss-cycle");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
