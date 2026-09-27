using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas del canal portal de sesión (acta A68): inicio, renovación y cierre por
/// <c>/auth/portal/*</c>, la cookie del refresh token, el header contra CSRF, el filtro de rol y la
/// auditoría con canal y causa interna. También cubren que las rutas del móvil solo acepten su cliente.
/// Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class PortalAuthApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string PortalLoginUrl = "/api/v1/auth/portal/login";
    private const string PortalRefreshUrl = "/api/v1/auth/portal/refresh";
    private const string PortalLogoutUrl = "/api/v1/auth/portal/logout";
    private const string CookieName = "cauce_portal_rt";
    private const string CsrfHeader = "X-Cauce-Portal";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public PortalAuthApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    // ----- Inicio de sesión -----

    [SkippableFact]
    public async Task Login_ActiveNutritionist_ReturnsAccessTokenWithoutRefreshAndSetsTheCookie()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await PortalLoginAsync(nutritionist.Email, Factory.TokenClient.ValidPassword);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("accessToken", "expiresIn", "tokenType", "user");
        root.GetProperty("user").GetProperty("role").GetString().Should().Be(UserRoles.Nutritionist);

        var cookie = SessionCookieHeader(response);
        cookie.Should().StartWith($"{CookieName}={Factory.TokenClient.ValidRefreshToken};");
        cookie.Should().ContainEquivalentOf("httponly");
        cookie.Should().ContainEquivalentOf("samesite=strict");
        cookie.Should().ContainEquivalentOf("path=/api/v1/auth/portal");
        // Testing corre con la configuración por defecto, que marca la cookie Secure.
        cookie.Should().ContainEquivalentOf("secure");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [SkippableFact]
    public async Task Login_ActiveNutritionist_AuditsLoginWithPortalChannel()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        await PortalLoginAsync(nutritionist.Email, Factory.TokenClient.ValidPassword);

        var log = (await AuditLogsAsync(action: AuditActionType.Login)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(nutritionist.Id);
        ContextValue(log, "channel").Should().Be("portal");
        ContextValue(log, "cause").Should().BeNull();
    }

    [SkippableFact]
    public async Task Login_Patient_Returns401RevokesTheSessionAndAuditsRoleNotAllowed()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PortalLoginAsync(patient.Email, Factory.TokenClient.ValidPassword);

        await AssertInvalidCredentialsAsync(response);
        Factory.TokenClient.LogoutCount.Should().Be(1, "la sesión que Keycloak abrió no puede quedar viva");
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
        await AssertFailedLoginAsync(AuditActionType.FailedLogin, "role_not_allowed", patient.Id);
    }

    [SkippableFact]
    public async Task Login_SuspendedNutritionist_Returns401AndAuditsSuspended()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        await SuspendAsync(nutritionist.Id);

        var response = await PortalLoginAsync(nutritionist.Email, Factory.TokenClient.ValidPassword);

        await AssertInvalidCredentialsAsync(response);
        await AssertFailedLoginAsync(AuditActionType.FailedLogin, "account_suspended", nutritionist.Id);
    }

    [SkippableFact]
    public async Task Login_WrongPassword_Returns401AndAuditsWrongPassword()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await PortalLoginAsync(nutritionist.Email, "Incorrecta#1");

        await AssertInvalidCredentialsAsync(response);
        await AssertFailedLoginAsync(AuditActionType.FailedLogin, "wrong_password", nutritionist.Id);
    }

    [SkippableFact]
    public async Task Login_DisabledInKeycloak_Returns401AndAuditsDisabled()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        Factory.KeycloakClient.DisabledUsers.Add(nutritionist.KeycloakId);

        var response = await PortalLoginAsync(nutritionist.Email, "Incorrecta#1");

        await AssertInvalidCredentialsAsync(response);
        await AssertFailedLoginAsync(AuditActionType.FailedLogin, "account_disabled", nutritionist.Id);
    }

    [SkippableFact]
    public async Task Login_PendingNutritionistWithoutPassword_Returns401AndAuditsPendingActivation()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync(active: false);

        var response = await PortalLoginAsync(nutritionist.Email, "Portal#2026");

        await AssertInvalidCredentialsAsync(response);
        await AssertFailedLoginAsync(AuditActionType.FailedLogin, "pending_activation", nutritionist.Id);
    }

    [SkippableFact]
    public async Task Login_UnknownEmail_Returns401AndAuditsUnknownAccountWithoutActor()
    {
        SkipIfUnavailable();

        var response = await PortalLoginAsync("nadie@cauce.local", "Portal#2026");

        await AssertInvalidCredentialsAsync(response);
        await AssertFailedLoginAsync(AuditActionType.FailedLogin, "unknown_account", actorId: null);
    }

    [SkippableFact]
    public async Task Login_LockedAccount_Returns423AndAuditsAccountLocked()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        var lockedUntil = DateTime.UtcNow.AddSeconds(60);
        Factory.KeycloakClient.LockUser(nutritionist.KeycloakId, lockedUntil);

        var response = await PortalLoginAsync(nutritionist.Email, "Incorrecta#1");

        // CP015: el bloqueo conserva su mensaje con el tiempo de espera, y queda como alerta de seguridad.
        response.StatusCode.Should().Be(HttpStatusCode.Locked);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("account_locked");
        document.RootElement.GetProperty("lockedUntil").GetDateTime().Should().BeCloseTo(lockedUntil, TimeSpan.FromSeconds(1));
        await AssertFailedLoginAsync(AuditActionType.AccountLocked, "account_locked", nutritionist.Id);
        (await AuditLogsAsync(action: AuditActionType.FailedLogin)).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task Login_MissingPassword_Returns400AndAuditsInvalidRequest()
    {
        SkipIfUnavailable();

        var response = await Factory.CreateClient().PostAsJsonAsync(PortalLoginUrl, new { email = "n@cauce.local" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertFailedLoginAsync(AuditActionType.FailedLogin, "invalid_request", actorId: null);
    }

    // ----- Rutas del móvil: solo cauce-mobile -----

    [SkippableFact]
    public async Task MobileLogin_WithPortalClient_Returns400UnsupportedClientAndAudits()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = nutritionist.Email,
            password = Factory.TokenClient.ValidPassword,
            clientId = "cauce-web-portal"
        });

        // Sin esta regla, /auth/login devolvería el refresh token en el cuerpo y saltearía el filtro de rol.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("unsupported_client");
        var log = await AssertFailedLoginAsync(AuditActionType.FailedLogin, "unsupported_client", nutritionist.Id);
        ContextValue(log, "channel").Should().Be("mobile");
    }

    [SkippableFact]
    public async Task MobileLogout_WithPortalClient_Returns400AndAuditsFailedLogout()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email).PostAsJsonAsync("/api/v1/auth/logout", new
        {
            refreshToken = "cualquier-cosa",
            clientId = "cauce-web-portal"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Factory.TokenClient.LogoutCount.Should().Be(0);
        var log = (await AuditLogsAsync(action: AuditActionType.FailedLogout)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(patient.Id);
        ContextValue(log, "channel").Should().Be("mobile");
        ContextValue(log, "cause").Should().Be("unsupported_client");
    }

    // ----- Renovación -----

    [SkippableFact]
    public async Task Refresh_WithoutCsrfHeader_Returns403WithoutReachingKeycloak()
    {
        SkipIfUnavailable();
        var client = CookielessClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, PortalRefreshUrl);
        request.Headers.Add("Cookie", $"{CookieName}={Factory.TokenClient.ValidRefreshToken}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("csrf_header_missing");
        (await AuditLogsAsync(action: AuditActionType.TokenRefresh)).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task Refresh_WithoutCookie_Returns401AndAuditsMissingCookie()
    {
        SkipIfUnavailable();

        var response = await PortalRefreshAsync(cookieValue: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("invalid_refresh_token");
        var log = (await AuditLogsAsync(action: AuditActionType.FailedTokenRefresh)).Should().ContainSingle().Subject;
        ContextValue(log, "channel").Should().Be("portal");
        ContextValue(log, "cause").Should().Be("missing_refresh_cookie");
    }

    [SkippableFact]
    public async Task Refresh_WithCookieAndHeader_RotatesTheCookieWithoutRefreshInTheBody()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        Factory.TokenClient.Subject = nutritionist.KeycloakId;

        var response = await PortalRefreshAsync(Factory.TokenClient.ValidRefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.TryGetProperty("refreshToken", out _).Should().BeFalse();
        document.RootElement.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        SessionCookieHeader(response).Should().StartWith($"{CookieName}={Factory.TokenClient.ValidRefreshToken}-rotated;");

        var log = (await AuditLogsAsync(action: AuditActionType.TokenRefresh)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(nutritionist.Id);
        ContextValue(log, "channel").Should().Be("portal");
    }

    [SkippableFact]
    public async Task Refresh_PatientCookie_Returns401AndAuditsRoleNotAllowed()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        Factory.TokenClient.Subject = patient.KeycloakId;

        var response = await PortalRefreshAsync(Factory.TokenClient.ValidRefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var log = (await AuditLogsAsync(action: AuditActionType.FailedTokenRefresh)).Should().ContainSingle().Subject;
        ContextValue(log, "cause").Should().Be("role_not_allowed");
    }

    // ----- Cierre -----

    [SkippableFact]
    public async Task Logout_WithBearerHeaderAndCookie_RevokesDeletesTheCookieAndAudits()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await PortalLogoutAsync(NutritionistBearer(nutritionist), withCsrfHeader: true);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Factory.TokenClient.LogoutCount.Should().Be(1);
        var cookie = SessionCookieHeader(response);
        cookie.Should().StartWith($"{CookieName}=;");
        cookie.Should().ContainEquivalentOf("expires=Thu, 01 Jan 1970");
        cookie.Should().ContainEquivalentOf("path=/api/v1/auth/portal");

        var log = (await AuditLogsAsync(action: AuditActionType.Logout)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(nutritionist.Id);
        ContextValue(log, "channel").Should().Be("portal");
    }

    [SkippableFact]
    public async Task Logout_WithoutCsrfHeader_Returns403AndAuditsFailedLogout()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await PortalLogoutAsync(NutritionistBearer(nutritionist), withCsrfHeader: false);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Factory.TokenClient.LogoutCount.Should().Be(0);
        var log = (await AuditLogsAsync(action: AuditActionType.FailedLogout)).Should().ContainSingle().Subject;
        ContextValue(log, "cause").Should().Be("csrf_header_missing");
    }

    [SkippableFact]
    public async Task Logout_WithoutBearer_Returns401()
    {
        SkipIfUnavailable();

        var response = await PortalLogoutAsync(bearer: null, withCsrfHeader: true);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Logout_AsPatient_Returns403()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PortalLogoutAsync(
            TestJwtBuilder.Build(patient.KeycloakId, patient.Email, UserRoles.Patient), withCsrfHeader: true);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----- Rate limiting (CP015) -----

    [SkippableTheory]
    [InlineData(PortalLoginUrl)]
    [InlineData("/api/v1/auth/login")]
    public async Task Login_SixConsecutiveFailures_NeverAnswer429(string url)
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        var client = Factory.CreateClient();

        // CP015: cinco intentos fallidos llevan al bloqueo y el sexto lo muestra. Si el rate limiting
        // respondiera 429 antes, el caso nunca llegaría al bloqueo de Keycloak.
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            var response = await client.PostAsJsonAsync(url, new
            {
                email = nutritionist.Email,
                password = "Incorrecta#1",
                clientId = "cauce-mobile"
            });

            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, $"el intento {attempt} no debe limitarse");
        }
    }

    [SkippableFact]
    public async Task PortalLogin_EleventhAttempt_Answers429WithoutConsumingTheMobileQuota()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();
        HttpResponseMessage? last = null;

        for (var attempt = 1; attempt <= 11; attempt++)
        {
            last = await client.PostAsJsonAsync(PortalLoginUrl, new { email = "nadie@cauce.local", password = "x" });
        }

        last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Contadores separados: agotar el del portal no deja sin login al móvil desde la misma IP.
        var mobile = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "nadie@cauce.local",
            password = "x",
            clientId = "cauce-mobile"
        });
        mobile.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    // ----- Ayudas -----

    private Task<HttpResponseMessage> PortalLoginAsync(string email, string password) =>
        Factory.CreateClient().PostAsJsonAsync(PortalLoginUrl, new { email, password });

    private async Task<HttpResponseMessage> PortalRefreshAsync(string? cookieValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, PortalRefreshUrl);
        request.Headers.Add(CsrfHeader, "1");
        if (cookieValue is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={cookieValue}");
        }

        return await CookielessClient().SendAsync(request);
    }

    private async Task<HttpResponseMessage> PortalLogoutAsync(string? bearer, bool withCsrfHeader)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, PortalLogoutUrl);
        request.Headers.Add("Cookie", $"{CookieName}={Factory.TokenClient.ValidRefreshToken}");
        if (withCsrfHeader)
        {
            request.Headers.Add(CsrfHeader, "1");
        }

        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await CookielessClient().SendAsync(request);
    }

    /// <summary>
    /// Cliente sin contenedor de cookies: la cookie del portal es <c>Secure</c> y el servidor de pruebas
    /// corre sobre http, así que el contenedor no la reenviaría. Las pruebas la mandan a mano.
    /// </summary>
    private HttpClient CookielessClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static string NutritionistBearer((Guid Id, string KeycloakId, string Email) nutritionist) =>
        TestJwtBuilder.Build(nutritionist.KeycloakId, nutritionist.Email, UserRoles.Nutritionist);

    private static string SessionCookieHeader(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Set-Cookie", out var values).Should().BeTrue("la respuesta debe fijar la cookie");
        return values!.Single(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal));
    }

    private static async Task AssertInvalidCredentialsAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("invalid_credentials");
        // El cliente recibe siempre el mismo mensaje: la causa queda solo en la auditoría.
        document.RootElement.GetProperty("detail").GetString().Should().Be("Credenciales inválidas.");
    }

    private async Task<AuditLog> AssertFailedLoginAsync(AuditActionType action, string cause, Guid? actorId)
    {
        var log = (await AuditLogsAsync(action: action)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(actorId);
        ContextValue(log, "cause").Should().Be(cause);
        return log;
    }

    private async Task SuspendAsync(Guid userId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Suspend();
        await db.SaveChangesAsync();
    }
}
