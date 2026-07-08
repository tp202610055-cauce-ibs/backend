using System.Net;
using System.Net.Http.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de integración del registro del token de FCM (TS10 CA01): persistencia del token y auditoría
/// explícita de la actualización. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class FcmTokenApiTests : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public FcmTokenApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task UpdateFcmToken_AsPatient_PersistsTokenAndAudits()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);

        var response = await client.PutAsJsonAsync("/api/v1/users/me/fcm-token", new { fcmToken = "device-token-xyz" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == patient.Id);
        user.FcmToken.Should().Be("device-token-xyz");

        var audit = (await AuditLogsAsync("User", patient.Id, AuditActionType.Update)).Should().ContainSingle().Subject;
        (audit.AdditionalContext ?? string.Empty).Should().Contain("fcm_token_updated");
    }

    [SkippableFact]
    public async Task UpdateFcmToken_NullBody_UnlinksToken()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);

        await client.PutAsJsonAsync("/api/v1/users/me/fcm-token", new { fcmToken = "to-be-cleared" });
        var clearResponse = await client.PutAsJsonAsync("/api/v1/users/me/fcm-token", new { fcmToken = (string?)null });

        clearResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == patient.Id);
        user.FcmToken.Should().BeNull();
    }
}
