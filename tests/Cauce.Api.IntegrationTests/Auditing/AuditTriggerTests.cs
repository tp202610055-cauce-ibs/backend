using System.Net;
using System.Net.Http.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
using Cauce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Auditing;

/// <summary>
/// Pruebas de la capa 4 de auditoría (DEC-B5-03): triggers PostgreSQL sobre las tablas críticas.
/// Verifican INSERT/UPDATE/DELETE, la propagación del actor keycloak→local vía <c>set_config</c>, el
/// origen <c>trigger</c>, y la inmutabilidad de <c>audit_logs</c>. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuditTriggerTests : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public AuditTriggerTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task DeclareAllergy_InsertTrigger_WritesCreateAuditWithActorAndSource()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var allergyId = await FirstActiveAllergyIdAsync();

        var response = await PostWithKey(
            PatientClient(patient.KeycloakId, patient.Email),
            "/api/v1/patients/allergies",
            new { allergyId, severity = "Moderate", notes = (string?)null });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var logs = await AuditLogsAsync("PatientAllergy");
        var log = logs.Should().ContainSingle().Subject;
        log.ActionType.Should().Be(AuditActionType.Create);
        log.ActorUserId.Should().Be(patient.Id);
        log.EntityId.Should().NotBeNull();
        log.NewValuesHash.Should().NotBeNullOrEmpty();
        log.AdditionalContext.Should().Contain("trigger");
    }

    [SkippableFact]
    public async Task RemoveAllergy_DeleteTrigger_WritesDeleteAuditWithOldHash()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var allergyId = await FirstActiveAllergyIdAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);

        var created = await PostWithKey(client, "/api/v1/patients/allergies",
            new { allergyId, severity = "Mild", notes = (string?)null });
        var body = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var patientAllergyId = body.GetProperty("patientAllergyId").GetGuid();

        var deleted = await client.DeleteAsync($"/api/v1/patients/allergies/{patientAllergyId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteLog = (await AuditLogsAsync("PatientAllergy", action: AuditActionType.Delete)).Should().ContainSingle().Subject;
        deleteLog.ActorUserId.Should().Be(patient.Id);
        deleteLog.OldValuesHash.Should().NotBeNullOrEmpty();
        deleteLog.NewValuesHash.Should().BeNull();
    }

    [SkippableFact]
    public async Task UpdateProfile_UpdateTrigger_WritesUpdateAuditWithActor()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PutAsJsonAsync("/api/v1/patients/profile", new { weightKg = 72.5m });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateLog = (await AuditLogsAsync("PatientProfile", action: AuditActionType.Update)).Should().ContainSingle().Subject;
        updateLog.ActorUserId.Should().Be(patient.Id);
        updateLog.OldValuesHash.Should().NotBeNullOrEmpty();
        updateLog.NewValuesHash.Should().NotBeNullOrEmpty();
    }

    [SkippableFact]
    public async Task CreateIbsSss_InsertTrigger_WritesCreateAudit()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PostWithKey(
            PatientClient(patient.KeycloakId, patient.Email),
            "/api/v1/ibs-sss",
            new
            {
                assessmentType = "Baseline",
                painSeverity = 50,
                painFrequency = 50,
                bloatingSeverity = 50,
                bowelHabitsDissatisfaction = 50,
                lifeInterference = 50
            });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var log = (await AuditLogsAsync("IbsSssAssessment", action: AuditActionType.Create)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(patient.Id);
        log.AdditionalContext.Should().Contain("trigger");
    }

    [SkippableFact]
    public async Task SeedRecommendationDirectly_InsertTrigger_WritesCreateWithNullActor()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var recommendationId = await SeedRecommendationAsync(patient.Id);

        // La inserción se hizo con el DbContext en un ámbito sin petición HTTP: el actor queda nulo
        // (acción del sistema), demostrando la propagación de un actor ausente.
        var log = (await AuditLogsAsync("Recommendation", recommendationId, AuditActionType.Create))
            .Should().ContainSingle().Subject;
        log.ActorUserId.Should().BeNull();
        log.AdditionalContext.Should().Contain("trigger");
    }

    [SkippableFact]
    public async Task AuditLogs_RejectsRawUpdate_Immutable()
    {
        SkipIfUnavailable();
        await SeedAuditRowAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var act = () => db.Database.ExecuteSqlRawAsync("UPDATE audit_logs SET action_type = 'login';");

        await act.Should().ThrowAsync<Exception>();
    }

    [SkippableFact]
    public async Task AuditLogs_RejectsRawDelete_Immutable()
    {
        SkipIfUnavailable();
        await SeedAuditRowAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var act = () => db.Database.ExecuteSqlRawAsync("DELETE FROM audit_logs;");

        await act.Should().ThrowAsync<Exception>();
    }

    private async Task SeedAuditRowAsync()
    {
        var patient = await SeedPatientAsync();
        var allergyId = await FirstActiveAllergyIdAsync();
        await PostWithKey(PatientClient(patient.KeycloakId, patient.Email), "/api/v1/patients/allergies",
            new { allergyId, severity = "Mild", notes = (string?)null });
    }

    private async Task<Guid> SeedRecommendationAsync(Guid patientId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var modelVersionId = await db.ModelVersions.AsNoTracking().Select(v => v.Id).FirstAsync();
        var foodId = await db.FoodItems.AsNoTracking().Select(f => f.Id).FirstAsync();

        var item = RecommendationItem.Create(foodId, ActionType.Suggest, "razón de prueba", null);
        var recommendation = Recommendation.Generate(
            patientId,
            modelVersionId,
            ConfidenceScore.Create(0.50m),
            ExplanationSource.LlmGenerated,
            "Explicación de prueba.",
            new[] { item },
            DateTime.UtcNow,
            TimeSpan.FromHours(72));

        db.Recommendations.Add(recommendation);
        await db.SaveChangesAsync();
        return recommendation.Id;
    }
}
