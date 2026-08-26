using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas del bloqueo de cuenta por intentos fallidos (US05 CA02). Verifican que el backend traduzca
/// el estado de fuerza bruta de Keycloak a un 423 con el momento de desbloqueo, en lugar del 401
/// genérico indistinguible de una contraseña incorrecta. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class LockoutTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string LoginUrl = "/api/v1/auth/login";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public LockoutTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Login_LockedAccount_Returns423WithLockedUntil()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var lockedUntil = DateTime.UtcNow.AddSeconds(60);
        Factory.KeycloakClient.LockUser(patient.KeycloakId, lockedUntil);
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginUrl, new
        {
            email = patient.Email,
            password = "ContrasenaIncorrecta1",
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Locked);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("errorCode").GetString().Should().Be("account_locked");
        root.GetProperty("status").GetInt32().Should().Be(423);
        root.TryGetProperty("lockedUntil", out var until).Should().BeTrue("US05 CA02 exige el tiempo de espera");
        until.GetDateTime().Should().BeCloseTo(lockedUntil, TimeSpan.FromSeconds(1));
    }

    [SkippableFact]
    public async Task Login_WrongPasswordWithoutLockout_Returns401InvalidCredentials()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginUrl, new
        {
            email = patient.Email,
            password = "ContrasenaIncorrecta1",
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("invalid_credentials");
        document.RootElement.TryGetProperty("lockedUntil", out _).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Login_UnknownEmail_Returns401WithoutRevealingExistence()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginUrl, new
        {
            email = "no-registrado@cauce.local",
            password = "ContrasenaIncorrecta1",
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("invalid_credentials");
    }

    [SkippableFact]
    public async Task Login_AdminApiUnavailable_DegradesTo401()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        Factory.KeycloakClient.LockUser(patient.KeycloakId, DateTime.UtcNow.AddSeconds(60));
        Factory.KeycloakClient.BruteForceFailure = new HttpRequestException("Admin API caída");
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginUrl, new
        {
            email = patient.Email,
            password = "ContrasenaIncorrecta1",
            clientId = "cauce-mobile"
        });

        // Aunque la cuenta esté bloqueada, si no se puede confirmar se degrada al comportamiento
        // anterior en lugar de responder un error del servidor.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("invalid_credentials");
    }
}
