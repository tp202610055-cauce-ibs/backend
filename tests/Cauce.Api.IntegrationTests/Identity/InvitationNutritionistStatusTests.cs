using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas del estado del nutricionista en el ciclo de los códigos de invitación (acta A53): el registro
/// con un código rechaza al nutricionista que no puede atender, igual que el canje posterior (acta A41), y
/// la generación exige una cuenta activa. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class InvitationNutritionistStatusTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string RegisterEndpoint = "/api/v1/auth/register";
    private const string InvitationsEndpoint = "/api/v1/invitations";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public InvitationNutritionistStatusTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Register_CodeOfSuspendedNutritionist_Returns409WithReasonAndCreatesNothing()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        var code = await SeedInvitationAsync(nutritionist.Id);
        await SuspendAsync(nutritionist.Id);
        var email = UniqueEmail();

        var response = await Factory.CreateClient().PostAsJsonAsync(RegisterEndpoint, RegisterBody(email, code));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("nutritionist_not_available");
        document.RootElement.GetProperty("reason").GetString().Should().Be("suspended");

        // El rechazo ocurre antes de crear nada: el código sigue disponible, no hay cuenta local y no se
        // creó el usuario en Keycloak, así que no hubo nada que compensar.
        (await CodeIsUnusedAsync(code)).Should().BeTrue();
        (await UserExistsAsync(email)).Should().BeFalse();
        (await Factory.KeycloakClient.FindByEmailAsync(email)).Should().BeNull();
    }

    [SkippableFact]
    public async Task Register_CodeOfActiveNutritionist_Returns201AndConsumesTheCode()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        var code = await SeedInvitationAsync(nutritionist.Id);

        var response = await Factory.CreateClient().PostAsJsonAsync(RegisterEndpoint, RegisterBody(UniqueEmail(), code));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await CodeIsUnusedAsync(code)).Should().BeFalse();
    }

    [SkippableFact]
    public async Task GenerateInvitation_SuspendedNutritionist_Returns409WithReasonAndIssuesNoCode()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        await SuspendAsync(nutritionist.Id);

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email)
            .PostAsync(InvitationsEndpoint, content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("nutritionist_not_available");
        document.RootElement.GetProperty("reason").GetString().Should().Be("suspended");
        (await InvitationCountAsync(nutritionist.Id)).Should().Be(0);
    }

    [SkippableFact]
    public async Task GenerateInvitation_PendingNutritionist_IsActivatedFirstAndReturns201()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync(active: false);

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email)
            .PostAsync(InvitationsEndpoint, content: null);

        // El behavior activa al nutricionista antes del handler (acta A51): para una cuenta pendiente, el
        // chequeo de la generación es solo respaldo.
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await StatusOfAsync(nutritionist.Id)).Should().Be(UserStatus.Active);
    }

    private static string UniqueEmail() => $"patient-{Guid.NewGuid():N}@cauce.local";

    private static object RegisterBody(string email, string invitationCode) => new
    {
        email,
        fullName = "Paciente Test",
        password = "Password1",
        consentDocumentVersion = CustomWebApplicationFactory.ConsentVersion,
        consentTextHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.ConsentText))).ToLowerInvariant(),
        invitationCode
    };

    private async Task<string> SeedInvitationAsync(Guid nutritionistId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var code = "INV" + new string(Guid.NewGuid().ToString("N").ToUpperInvariant()
            .Where(char.IsLetterOrDigit).Take(7).ToArray());
        db.InvitationCodes.Add(
            InvitationCode.Generate(Guid.NewGuid(), code, nutritionistId, DateTime.UtcNow, InvitationCode.Validity));
        await db.SaveChangesAsync();
        return code;
    }

    private async Task SuspendAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Suspend();
        await db.SaveChangesAsync();
    }

    private async Task<bool> CodeIsUnusedAsync(string code)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var invitation = await db.InvitationCodes.AsNoTracking().FirstAsync(i => i.Code == code);
        return invitation.UsedByPatientId is null;
    }

    private async Task<bool> UserExistsAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        return await db.Users.AsNoTracking().AnyAsync(u => u.Email == email);
    }

    private async Task<int> InvitationCountAsync(Guid nutritionistId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        return await db.InvitationCodes.AsNoTracking().CountAsync(i => i.NutritionistId == nutritionistId);
    }

    private async Task<UserStatus> StatusOfAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        return await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Status).SingleAsync();
    }
}
