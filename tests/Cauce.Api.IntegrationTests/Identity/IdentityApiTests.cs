using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de integración end-to-end del módulo de identidad. Usan PostgreSQL real
/// (Testcontainers) y dobles en memoria de Keycloak y correo. Si Docker no está
/// disponible, las pruebas se omiten.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IdentityApiTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private CustomWebApplicationFactory _factory = null!;

    /// <summary>
    /// Inicializa la prueba con el fixture de PostgreSQL.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    public IdentityApiTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (!_postgres.IsAvailable)
        {
            return;
        }

        _factory = new CustomWebApplicationFactory(_postgres.ConnectionString);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<UserRolesSeeder>().SeedAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    // ----- Registro de paciente -----

    [SkippableFact]
    public async Task Register_WithValidData_Returns201AndPersistsUserAndConsent()
    {
        SkipIfNoDocker();
        var client = _factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", RegisterBody(email));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);
        user.Should().NotBeNull();
        var consent = await db.ConsentRecords.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == user!.Id);
        consent.Should().NotBeNull();
        consent!.IsCurrent.Should().BeTrue();
        _factory.KeycloakClient.VerifyEmailsSent.Should().NotBeEmpty();
        var hasAudit = await db.AuditLogs.AsNoTracking().AnyAsync(a => a.ActionType == AuditActionType.Register);
        hasAudit.Should().BeTrue();
    }

    [SkippableFact]
    public async Task Register_WithMismatchedConsentHash_Returns400()
    {
        SkipIfNoDocker();
        var client = _factory.CreateClient();
        var body = new
        {
            email = UniqueEmail(),
            fullName = "Paciente Test",
            password = "Password1",
            consentDocumentVersion = CustomWebApplicationFactory.ConsentVersion,
            consentTextHash = new string('b', 64),
            invitationCode = (string?)null
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        SkipIfNoDocker();
        var client = _factory.CreateClient();
        var email = UniqueEmail();

        var first = await client.PostAsJsonAsync("/api/v1/auth/register", RegisterBody(email));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register", RegisterBody(email));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [SkippableFact]
    public async Task Register_WithInvalidInvitationCode_Returns400()
    {
        SkipIfNoDocker();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", RegisterBody(UniqueEmail(), "NONEXISTENT9"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Register_WithExpiredInvitationCode_Returns400()
    {
        SkipIfNoDocker();
        var (nutritionistId, _) = await SeedNutritionistAsync(UniqueEmail());
        var code = await SeedInvitationAsync(nutritionistId, expired: true, consumedBy: null);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", RegisterBody(UniqueEmail(), code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Register_WithAlreadyUsedInvitationCode_Returns400()
    {
        SkipIfNoDocker();
        var (nutritionistId, _) = await SeedNutritionistAsync(UniqueEmail());
        var (patientId, _) = await SeedPatientAsync(UniqueEmail());
        var code = await SeedInvitationAsync(nutritionistId, expired: false, consumedBy: patientId);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", RegisterBody(UniqueEmail(), code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ----- Restablecimiento de contraseña -----

    [SkippableFact]
    public async Task RequestPasswordReset_WithExistingEmail_Returns200AndCreatesToken()
    {
        SkipIfNoDocker();
        var (userId, _) = await SeedPatientAsync(UniqueEmail());
        var email = await EmailOf(userId);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/password-reset/request", new { email });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        (await db.PasswordResetTokens.AsNoTracking().AnyAsync(t => t.UserId == userId)).Should().BeTrue();
        _factory.EmailSender.SentEmails.Should().Contain(e => e.Kind == "password-reset" && e.Recipient == email);
    }

    [SkippableFact]
    public async Task RequestPasswordReset_WithNonExistentEmail_Returns200AndDoesNotCreateToken()
    {
        SkipIfNoDocker();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/password-reset/request", new { email = UniqueEmail() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.EmailSender.SentEmails.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task ConfirmPasswordReset_WithValidToken_Returns200AndConsumesToken()
    {
        SkipIfNoDocker();
        var (userId, _) = await SeedPatientAsync(UniqueEmail());
        var email = await EmailOf(userId);
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/password-reset/request", new { email });
        var link = _factory.EmailSender.SentEmails.First(e => e.Kind == "password-reset").Payload;
        var token = ExtractToken(link);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm", new { token, newPassword = "NewPass1" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        (await db.PasswordResetTokens.AsNoTracking().AnyAsync(t => t.UserId == userId && t.IsUsed)).Should().BeTrue();
    }

    [SkippableFact]
    public async Task ConfirmPasswordReset_WithExpiredToken_Returns400()
    {
        SkipIfNoDocker();
        var (userId, _) = await SeedPatientAsync(UniqueEmail());
        const string plain = "expired-plain-token";
        await SeedResetTokenAsync(userId, plain, expired: true, used: false);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm", new { token = plain, newPassword = "NewPass1" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task ConfirmPasswordReset_WithAlreadyUsedToken_Returns400()
    {
        SkipIfNoDocker();
        var (userId, _) = await SeedPatientAsync(UniqueEmail());
        const string plain = "used-plain-token";
        await SeedResetTokenAsync(userId, plain, expired: false, used: true);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm", new { token = plain, newPassword = "NewPass1" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Los antiguos endpoints /auth/sessions (RegisterLoginEvent/RegisterLogoutEvent) se eliminaron en
    // el Prompt 5 (acta A3). El login/logout passthrough y su auditoría se cubren en AuditMiddlewareTests.

    // ----- Invitaciones -----

    [SkippableFact]
    public async Task Invitations_Post_AsNutritionist_Returns201()
    {
        SkipIfNoDocker();
        var (userId, keycloakId) = await SeedNutritionistAsync(UniqueEmail());
        var client = AuthenticatedClient(keycloakId, await EmailOf(userId), UserRoles.Nutritionist);

        var response = await client.PostAsync("/api/v1/invitations", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [SkippableFact]
    public async Task Invitations_Post_AsPatient_Returns403()
    {
        SkipIfNoDocker();
        var client = AuthenticatedClient(Guid.NewGuid().ToString(), UniqueEmail(), UserRoles.Patient);

        var response = await client.PostAsync("/api/v1/invitations", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----- Admin -----

    [SkippableFact]
    public async Task AdminNutritionists_Post_WithValidApiKey_Returns201()
    {
        SkipIfNoDocker();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Api-Key", CustomWebApplicationFactory.AdminApiKey);

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/nutritionists", new { email = UniqueEmail(), fullName = "Nutri Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _factory.EmailSender.SentEmails.Should().Contain(e => e.Kind == "credentials");
    }

    [SkippableFact]
    public async Task AdminNutritionists_Post_WithInvalidApiKey_Returns401()
    {
        SkipIfNoDocker();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Api-Key", "wrong-key");

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/nutritionists", new { email = UniqueEmail(), fullName = "Nutri Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task AdminNutritionists_Post_WithoutApiKey_Returns401()
    {
        SkipIfNoDocker();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/nutritionists", new { email = UniqueEmail(), fullName = "Nutri Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ----- Helpers -----

    private void SkipIfNoDocker()
    {
        Skip.IfNot(_postgres.IsAvailable, "Docker no está disponible; se omiten las pruebas de integración.");
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@cauce.local";

    private static object RegisterBody(string email, string? invitationCode = null) => new
    {
        email,
        fullName = "Paciente Test",
        password = "Password1",
        consentDocumentVersion = CustomWebApplicationFactory.ConsentVersion,
        consentTextHash = ConsentHash(),
        invitationCode
    };

    private static string ConsentHash()
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.ConsentText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ExtractToken(string link)
    {
        var query = new Uri(link).Query;
        var parts = query.Split("token=", StringSplitOptions.RemoveEmptyEntries);
        return parts[^1];
    }

    private HttpClient AuthenticatedClient(string keycloakId, string email, string role)
    {
        var client = _factory.CreateClient();
        var token = TestJwtBuilder.Build(keycloakId, email, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(Guid Id, string KeycloakId)> SeedPatientAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Patient).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId, email, "Paciente Seed", roleId);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, keycloakId);
    }

    private async Task<(Guid Id, string KeycloakId)> SeedNutritionistAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Nutritionist).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var user = User.CreateNutritionist(Guid.NewGuid(), keycloakId, email, "Nutri Seed", roleId);
        // La fábrica lo crea pendiente (acta A51); estas pruebas necesitan uno operativo.
        user.Activate();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, keycloakId);
    }

    private async Task<string> SeedInvitationAsync(Guid nutritionistId, bool expired, Guid? consumedBy)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var code = "TST" + new string(RandomNumberGenerator.GetItems<char>("ABCDEFGHJKMNPQRSTUVWXYZ23456789".AsSpan(), 7));
        var issuedAt = expired ? DateTime.UtcNow.AddHours(-73) : DateTime.UtcNow;
        var invitation = InvitationCode.Generate(Guid.NewGuid(), code, nutritionistId, issuedAt, InvitationCode.Validity);
        if (consumedBy is not null)
        {
            invitation.MarkAsUsed(consumedBy.Value, issuedAt.AddMinutes(1));
        }

        db.InvitationCodes.Add(invitation);
        await db.SaveChangesAsync();
        return code;
    }

    private async Task SeedResetTokenAsync(Guid userId, string plainToken, bool expired, bool used)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var issuedAt = expired ? DateTime.UtcNow.AddMinutes(-31) : DateTime.UtcNow;
        var token = PasswordResetToken.Issue(Guid.NewGuid(), userId, Hash(plainToken), issuedAt, PasswordResetToken.Validity);
        if (used)
        {
            token.Consume(issuedAt.AddMinutes(1));
        }

        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync();
    }

    private async Task<string> EmailOf(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        return await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Email).FirstAsync();
    }
}
