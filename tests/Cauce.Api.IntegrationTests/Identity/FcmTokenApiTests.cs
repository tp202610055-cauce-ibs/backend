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
public sealed class FcmTokenApiTests : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
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

    [SkippableFact]
    public async Task UpdateFcmToken_SameTokenFromSecondAccount_UnlinksItFromTheFirst()
    {
        SkipIfUnavailable();
        var first = await SeedPatientAsync();
        var second = await SeedPatientAsync();

        await PatientClient(first.KeycloakId, first.Email)
            .PutAsJsonAsync("/api/v1/users/me/fcm-token", new { fcmToken = "same-phone-token" });
        var response = await PatientClient(second.KeycloakId, second.Email)
            .PutAsJsonAsync("/api/v1/users/me/fcm-token", new { fcmToken = "same-phone-token" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Users.AsNoTracking().FirstAsync(u => u.Id == first.Id)).FcmToken.Should().BeNull();
        (await db.Users.AsNoTracking().FirstAsync(u => u.Id == second.Id)).FcmToken.Should().Be("same-phone-token");

        var moved = (await AuditLogsAsync("User", first.Id, AuditActionType.Update))
            .Select(audit => audit.AdditionalContext ?? string.Empty)
            .ToList();
        moved.Should().Contain(context => context.Contains("fcm_token_moved_to_another_account"));
        moved.Should().NotContain(context => context.Contains("same-phone-token"));
    }

    [SkippableFact]
    public async Task UpdateFcmToken_TokenLongerThanTheColumn_ReturnsBadRequest()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PutAsJsonAsync("/api/v1/users/me/fcm-token", new { fcmToken = new string('a', 501) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
