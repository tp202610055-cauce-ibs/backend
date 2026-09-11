using System.Net;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Patients;

/// <summary>
/// Pruebas de integración de la eliminación (anonimización) de cuenta del paciente (US26, Ley N° 29733):
/// happy path, gate del piloto activo (CA02), preservación de <c>consent_records</c>, deshabilitación en
/// Keycloak y bitácora de auditoría. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AccountDeletionApiTests : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public AccountDeletionApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task DeleteMyAccount_NotInPilot_AnonymizesDisablesKeycloakAndAudits()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedConsentAsync(patient.Id);

        var client = PatientClient(patient.KeycloakId, patient.Email);
        var response = await client.DeleteAsync("/api/v1/patients/me?confirmedActivePilotAcknowledged=false");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var user = await GetUserAsync(patient.Id);
        user.Email.Should().Be($"deleted-{patient.Id}@anonymized.local");
        user.FullName.Should().Be("Usuario anonimizado");
        user.Status.Should().Be(Cauce.Domain.Identity.Enums.UserStatus.Inactive);

        Factory.KeycloakClient.DisabledUsers.Should().Contain(patient.KeycloakId);

        // El registro de consentimiento se conserva intacto (trigger de inmutabilidad).
        (await ConsentCountAsync(patient.Id)).Should().Be(1);

        var log = (await AuditLogsAsync("User", patient.Id, AuditActionType.Delete)).Should().ContainSingle().Subject;
        (log.AdditionalContext ?? string.Empty).Should().Contain("anonymization").And.Contain("disable");

        Factory.EmailSender.SentEmails.Should().Contain(e => e.Kind == "account-deletion" && e.Recipient == patient.Email);
    }

    [SkippableFact]
    public async Task DeleteMyAccount_InActivePilotWithoutAcknowledgement_Returns409AndKeepsAccount()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await EnrollInPilotAsync(patient.Id);

        var client = PatientClient(patient.KeycloakId, patient.Email);
        var response = await client.DeleteAsync("/api/v1/patients/me?confirmedActivePilotAcknowledged=false");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var user = await GetUserAsync(patient.Id);
        user.Email.Should().Be(patient.Email);
        user.Status.Should().Be(Cauce.Domain.Identity.Enums.UserStatus.PendingActivation);
        Factory.KeycloakClient.DisabledUsers.Should().NotContain(patient.KeycloakId);
    }

    [SkippableFact]
    public async Task DeleteMyAccount_InActivePilotWithAcknowledgement_Anonymizes()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await EnrollInPilotAsync(patient.Id);

        var client = PatientClient(patient.KeycloakId, patient.Email);
        var response = await client.DeleteAsync("/api/v1/patients/me?confirmedActivePilotAcknowledged=true");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(patient.Id)).Status.Should().Be(Cauce.Domain.Identity.Enums.UserStatus.Inactive);
        Factory.KeycloakClient.DisabledUsers.Should().Contain(patient.KeycloakId);
    }

    private async Task SeedConsentAsync(Guid userId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var consent = ConsentRecord.Capture(Guid.NewGuid(), userId, "1.0", new string('a', 64), "127.0.0.1", DateTime.UtcNow);
        db.Set<ConsentRecord>().Add(consent);
        await db.SaveChangesAsync();
    }

    private async Task EnrollInPilotAsync(Guid userId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.EnrollInActivePilot();
        await db.SaveChangesAsync();
    }

    private async Task<User> GetUserAsync(Guid userId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
    }

    private async Task<int> ConsentCountAsync(Guid userId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Set<ConsentRecord>().AsNoTracking().CountAsync(c => c.UserId == userId);
    }
}
