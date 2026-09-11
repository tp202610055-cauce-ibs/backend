using System.Net;
using System.Net.Http.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Auditing;

/// <summary>
/// Pruebas de la capa 1 de auditoría (DEC-B5-01): el <c>AuditingMiddleware</c> registra LOGIN,
/// FAILED_LOGIN y LOGOUT resolviendo el actor (por correo en login, por principal en logout).
/// Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuditMiddlewareTests : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public AuditMiddlewareTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Login_ValidCredentials_WritesLoginAuditWithActor()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = patient.Email,
            password = Factory.TokenClient.ValidPassword,
            clientId = "cauce-mobile"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var log = (await AuditLogsAsync(action: AuditActionType.Login)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(patient.Id);
    }

    [SkippableFact]
    public async Task Login_InvalidCredentials_Returns401AndWritesFailedLoginAudit()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = patient.Email,
            password = "WrongPass123!",
            clientId = "cauce-mobile"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var log = (await AuditLogsAsync(action: AuditActionType.FailedLogin)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(patient.Id);
    }

    [SkippableFact]
    public async Task Logout_Authenticated_WritesLogoutAuditWithActor()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/logout", new
        {
            refreshToken = "fake-refresh-token",
            clientId = "cauce-mobile"
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var log = (await AuditLogsAsync(action: AuditActionType.Logout)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(patient.Id);
    }

    [SkippableFact]
    public async Task NonAuthEndpoint_DoesNotWriteAuthenticationAudit()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await AuditLogsAsync(action: AuditActionType.Login)).Should().BeEmpty();
        (await AuditLogsAsync(action: AuditActionType.Logout)).Should().BeEmpty();
        (await AuditLogsAsync(action: AuditActionType.FailedLogin)).Should().BeEmpty();
    }
}
