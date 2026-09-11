using System.Net;
using System.Net.Http.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Auditing;

/// <summary>
/// Pruebas de la capa 2 de auditoría (DEC-B5-01, acta A8): el <c>AuditingBehavior</c> audita
/// exactamente una vez los comandos marcados con <c>IAuditableCommand</c> (solo tablas SIN trigger) y
/// no duplica con los triggers. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuditBehaviorTests : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public AuditBehaviorTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task GenerateInvitationCode_WritesExactlyOneBehaviorAudit()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await PostWithKey(
            NutritionistClient(nutritionist.KeycloakId, nutritionist.Email),
            "/api/v1/invitations",
            content: null);
        response.IsSuccessStatusCode.Should().BeTrue();

        var logs = await AuditLogsAsync("InvitationCode");
        var log = logs.Should().ContainSingle().Subject;
        log.ActionType.Should().Be(AuditActionType.Register);
        log.ActorUserId.Should().Be(nutritionist.Id);
    }

    [SkippableFact]
    public async Task RequestPasswordReset_ExistingEmail_WritesExactlyOneBehaviorAudit()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/password-reset/request", new { email = patient.Email });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var log = (await AuditLogsAsync("User", action: AuditActionType.PasswordResetRequest)).Should().ContainSingle().Subject;
        // El behavior fija entity_id=null (acta A11/ajuste 1); registra el hash del payload.
        log.EntityId.Should().BeNull();
        log.NewValuesHash.Should().NotBeNullOrEmpty();
    }

    [SkippableFact]
    public async Task ConfirmPasswordReset_WritesOneAuditWithoutLeakingToken()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = Factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/password-reset/request", new { email = patient.Email });
        var token = ExtractResetToken();

        var confirm = await client.PostAsJsonAsync("/api/v1/auth/password-reset/confirm",
            new { token, newPassword = "NewPass123!" });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        var log = (await AuditLogsAsync("User", action: AuditActionType.PasswordResetConfirm)).Should().ContainSingle().Subject;
        // Payload redactado (acta A11): el hash es el del payload constante; el contexto no lleva el token.
        log.NewValuesHash.Should().NotBeNullOrEmpty();
        log.AdditionalContext.Should().BeNull();
        (log.NewValuesHash ?? string.Empty).Should().NotContain(token);
    }

    [SkippableFact]
    public async Task TriggeredTable_IsAuditedByTriggerOnly_NoBehaviorDuplication()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var allergyId = await FirstActiveAllergyIdAsync();

        await PostWithKey(PatientClient(patient.KeycloakId, patient.Email), "/api/v1/patients/allergies",
            new { allergyId, severity = "Moderate", notes = (string?)null });

        // Exactamente una fila para patient_allergies, proveniente del trigger (source='trigger'),
        // no del behavior (patient_allergies está triggerizada, no marcada IAuditableCommand).
        var logs = await AuditLogsAsync("PatientAllergy");
        var log = logs.Should().ContainSingle().Subject;
        log.AdditionalContext.Should().Contain("trigger");
    }

    private string ExtractResetToken()
    {
        var email = Factory.EmailSender.SentEmails.Should().ContainSingle(e => e.Kind == "password-reset").Subject;
        var marker = "token=";
        var index = email.Payload.IndexOf(marker, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0);
        var raw = email.Payload[(index + marker.Length)..];
        return Uri.UnescapeDataString(raw);
    }
}
