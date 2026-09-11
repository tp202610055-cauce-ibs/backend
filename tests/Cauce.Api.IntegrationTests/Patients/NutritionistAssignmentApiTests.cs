using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Patients;

/// <summary>
/// Pruebas de <c>POST /api/v1/patients/me/nutritionist-assignment</c> (acta A41), el canje de un código
/// de invitación después del registro. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class NutritionistAssignmentApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string Endpoint = "/api/v1/patients/me/nutritionist-assignment";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public NutritionistAssignmentApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Assign_ValidCode_ReturnsCreatedAndLinksThePatient()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        var code = await SeedInvitationAsync(nutritionist.Id);

        var response = await PatientClient(patient.KeycloakId).PostAsJsonAsync(Endpoint, new { invitationCode = code });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("nutritionistId").GetGuid().Should().Be(nutritionist.Id);

        // El vínculo real debe existir, no solo el código consumido.
        (await ActiveAssignmentExistsAsync(patient.Id)).Should().BeTrue();
    }

    [SkippableFact]
    public async Task Assign_SecondRedemption_ReturnsConflict()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        var client = PatientClient(patient.KeycloakId);

        var first = await SeedInvitationAsync(nutritionist.Id);
        (await client.PostAsJsonAsync(Endpoint, new { invitationCode = first })).StatusCode
            .Should().Be(HttpStatusCode.Created);

        var second = await SeedInvitationAsync(nutritionist.Id);
        var response = await client.PostAsJsonAsync(Endpoint, new { invitationCode = second });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeOfAsync(response)).Should().Be("patient_already_assigned");
    }

    [SkippableFact]
    public async Task Assign_SuspendedNutritionist_ReturnsConflictWithReasonAndKeepsTheCodeUnused()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        var code = await SeedInvitationAsync(nutritionist.Id);
        await SuspendUserAsync(nutritionist.Id);

        var response = await PatientClient(patient.KeycloakId).PostAsJsonAsync(Endpoint, new { invitationCode = code });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("errorCode").GetString().Should().Be("nutritionist_not_available");
        root.GetProperty("reason").GetString().Should().Be("suspended");

        // El invariante de D11: el código sigue disponible para reemitirse.
        (await CodeIsUnusedAsync(code)).Should().BeTrue();
        (await ActiveAssignmentExistsAsync(patient.Id)).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Assign_UnknownCode_ReturnsBadRequest()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId)
            .PostAsJsonAsync(Endpoint, new { invitationCode = "ZZZZZZZZ" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeOfAsync(response)).Should().Be("invalid_invitation_code");
    }

    [SkippableFact]
    public async Task Assign_MalformedCode_ReturnsBadRequest()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId)
            .PostAsJsonAsync(Endpoint, new { invitationCode = "corto" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Assign_MalformedJson_ReturnsBadRequest()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId).PostAsync(
            Endpoint,
            new StringContent("{ esto no es json", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Assign_WithoutToken_ReturnsUnauthorized()
    {
        SkipIfUnavailable();

        var response = await Factory.CreateClient()
            .PostAsJsonAsync(Endpoint, new { invitationCode = "ABCDEFGH" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Assign_WithNutritionistToken_ReturnsForbidden()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId)
            .PostAsJsonAsync(Endpoint, new { invitationCode = "ABCDEFGH" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----- Apoyo -----

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

    private async Task SuspendUserAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.Suspend();
        await db.SaveChangesAsync();
    }

    private async Task<bool> ActiveAssignmentExistsAsync(Guid patientId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        return await db.NutritionistPatients
            .AnyAsync(a => a.PatientId == patientId && a.Status == AssignmentStatus.Active);
    }

    private async Task<bool> CodeIsUnusedAsync(string code)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var invitation = await db.InvitationCodes.FirstAsync(i => i.Code == code);
        return invitation.UsedByPatientId is null;
    }

    private static async Task<string?> ErrorCodeOfAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }
}
