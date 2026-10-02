using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de la normalización del correo (acta A70). Keycloak guarda y compara el correo en minúsculas,
/// así que la base local tiene que usar el mismo criterio: si no, quien se registra con una capitalización
/// y entra con otra recibe <c>user_local_missing</c> aunque su cuenta exista. El doble de Keycloak acepta
/// cualquier capitalización, igual que el real. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class EmailCaseInsensitivityTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public EmailCaseInsensitivityTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Login_WithOtherCapitalizationThanRegistration_Returns200()
    {
        SkipIfUnavailable();
        var mixedCase = MixedCaseEmail();
        var client = Factory.CreateClient();
        (await RegisterAsync(client, mixedCase)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await LoginAsync(client, mixedCase.ToLowerInvariant());

        response.StatusCode.Should().Be(HttpStatusCode.OK, "la cuenta existe aunque se escriba con otra capitalización");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("user").GetProperty("email").GetString()
            .Should().Be(mixedCase.ToLowerInvariant());
    }

    [SkippableFact]
    public async Task Login_WithUppercaseAfterLowercaseRegistration_Returns200()
    {
        SkipIfUnavailable();
        var lowercase = MixedCaseEmail().ToLowerInvariant();
        var client = Factory.CreateClient();
        (await RegisterAsync(client, lowercase)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await LoginAsync(client, lowercase.ToUpperInvariant());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Register_SameEmailWithOtherCapitalization_Returns409DuplicateEmail()
    {
        SkipIfUnavailable();
        var email = MixedCaseEmail();
        var client = Factory.CreateClient();
        (await RegisterAsync(client, email.ToLowerInvariant())).StatusCode.Should().Be(HttpStatusCode.Created);

        // Sin normalizar, la base local no lo reconoce como duplicado y el alta llega a Keycloak, que sí lo
        // rechaza: el cliente recibía un 502 genérico en lugar del 409 que puede explicar.
        var response = await RegisterAsync(client, email.ToUpperInvariant());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("duplicate_email");
    }

    [SkippableFact]
    public async Task Register_StoresTheEmailTrimmedAndLowercase()
    {
        SkipIfUnavailable();
        var email = MixedCaseEmail();
        var client = Factory.CreateClient();

        var response = await RegisterAsync(client, $"  {email} ");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var stored = await db.Users.AsNoTracking().Select(user => user.Email).ToListAsync();
        stored.Should().Contain(email.ToLowerInvariant());
    }

    [SkippableFact]
    public async Task RequestPasswordReset_WithOtherCapitalization_CreatesTheToken()
    {
        SkipIfUnavailable();
        var email = MixedCaseEmail();
        var client = Factory.CreateClient();
        (await RegisterAsync(client, email)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/password-reset/request",
            new { email = email.ToLowerInvariant(), clientId = "cauce-mobile" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.PasswordResetTokens.AsNoTracking().AnyAsync()).Should().BeTrue();
    }

    [SkippableFact]
    public async Task ResendVerification_WithOtherCapitalization_RequestsTheEmailAgain()
    {
        SkipIfUnavailable();
        var email = MixedCaseEmail();
        var client = Factory.CreateClient();
        (await RegisterAsync(client, email)).StatusCode.Should().Be(HttpStatusCode.Created);
        var sentAtRegistration = Factory.KeycloakClient.VerifyEmailsSent.Count;

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/verification-email/resend",
            new { email = email.ToUpperInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Factory.KeycloakClient.VerifyEmailsSent.Should().HaveCount(sentAtRegistration + 1);
    }

    [SkippableFact]
    public async Task Login_WithOtherCapitalization_AuditsTheResolvedActor()
    {
        SkipIfUnavailable();
        var email = MixedCaseEmail();
        var client = Factory.CreateClient();
        (await RegisterAsync(client, email)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await LoginAsync(client, email.ToUpperInvariant())).StatusCode.Should().Be(HttpStatusCode.OK);

        var log = (await AuditLogsAsync(action: AuditActionType.Login)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().NotBeNull("el middleware resuelve el actor con el mismo criterio que el login");
    }

    private static string MixedCaseEmail() => $"Ana.Mixta-{Guid.NewGuid():N}@Cauce.Local";

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            fullName = "Ana Mixta",
            password = "Password1",
            consentDocumentVersion = CustomWebApplicationFactory.ConsentVersion,
            consentTextHash = ConsentHash()
        });

    private Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = Factory.TokenClient.ValidPassword,
            clientId = "cauce-mobile"
        });

    private static string ConsentHash()
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.ConsentText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
