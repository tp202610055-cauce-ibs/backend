using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de <c>POST /api/v1/auth/refresh</c>. Verifican la renovación de sesión y su registro en la
/// bitácora de auditoría, que en este endpoint escribe el handler y no el middleware. Requieren
/// Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class RefreshEndpointTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string RefreshUrl = "/api/v1/auth/refresh";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public RefreshEndpointTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Refresh_ValidToken_Returns200WithRotatedTokensAndUser()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        Factory.TokenClient.Subject = patient.KeycloakId;
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(RefreshUrl, new
        {
            refreshToken = Factory.TokenClient.ValidRefreshToken,
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        // El realm rota los refresh tokens: el emitido debe ser distinto del presentado.
        root.GetProperty("refreshToken").GetString().Should().NotBe(Factory.TokenClient.ValidRefreshToken);
        root.GetProperty("user").GetProperty("userId").GetGuid().Should().Be(patient.Id);
    }

    [SkippableFact]
    public async Task Refresh_ValidToken_WritesTokenRefreshAuditWithActor()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        Factory.TokenClient.Subject = patient.KeycloakId;
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(RefreshUrl, new
        {
            refreshToken = Factory.TokenClient.ValidRefreshToken,
            clientId = "cauce-mobile"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var log = (await AuditLogsAsync(action: AuditActionType.TokenRefresh)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(patient.Id);
    }

    [SkippableFact]
    public async Task Refresh_InvalidToken_Returns401WithInvalidRefreshTokenErrorCode()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(RefreshUrl, new
        {
            refreshToken = "token-vencido-o-revocado",
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("invalid_refresh_token");
    }

    [SkippableFact]
    public async Task Refresh_InvalidToken_WritesFailedTokenRefreshAudit()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(RefreshUrl, new
        {
            refreshToken = "token-vencido-o-revocado",
            clientId = "cauce-mobile"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var log = (await AuditLogsAsync(action: AuditActionType.FailedTokenRefresh)).Should().ContainSingle().Subject;
        // Sin actor: el token rechazado no permite resolver la cuenta. La fila vale por IP y momento.
        log.ActorUserId.Should().BeNull();
    }

    [SkippableFact]
    public async Task Refresh_UnknownClientId_Returns400ValidationError()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(RefreshUrl, new
        {
            refreshToken = "cualquier-cosa",
            clientId = "cliente-inventado"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("validation_error");
        document.RootElement.GetProperty("errors").TryGetProperty("clientId", out _).Should().BeTrue();
    }
}
