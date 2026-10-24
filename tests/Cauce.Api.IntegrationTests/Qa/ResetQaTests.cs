using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Pruebas de <c>reset-qa</c> sobre el manifiesto publicado: casos consumidos, cuentas compartidas, cuentas que no
/// deben existir, activación pendiente y reanclaje del reloj. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ResetQaTests
    : QaCommandTestBase, IAsyncLifetime, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    public ResetQaTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Available)
        {
            await SeedConsentDocumentAsync();
            (await RunAsync("seed-qa")).Should().Be(CommandRunner.Success);
        }
    }

    [SkippableFact]
    public async Task ResetQa_PasswordChangedInCp082_RecreatesTheAccountWithTheQaPassword()
    {
        SkipIfUnavailable();
        var before = await UserAsync("paciente.reset3@ejemplo.com");

        var code = await RunCapturingAsync("reset-qa", "CP082");

        code.Should().Be(CommandRunner.Success, CauceLog);
        var after = await UserAsync("paciente.reset3@ejemplo.com");
        after.Id.Should().NotBe(before.Id);
        after.Status.Should().Be(UserStatus.Active);
        after.PatientCode.Should().NotBe(before.PatientCode);
        Factory.KeycloakClient.DeletedUsers.Should().Contain(before.KeycloakId);
        Factory.KeycloakClient.PasswordResets.Should().Contain(after.KeycloakId);
        var retired = await UserByIdAsync(before.Id);
        retired.Status.Should().Be(UserStatus.Inactive);
        retired.Email.Should().EndWith("@anonymized.local");
        CapturedLog.Should().Contain(line => line.Contains("Reset CP082"));
        CapturedLog.Should().NotContain(line => line.Contains(PatientPassword));
    }

    [SkippableFact]
    public async Task ResetQa_AccountDeletedInCp066_ComesBackWithItsData()
    {
        SkipIfUnavailable();
        var before = await UserAsync("paciente.eliminar@ejemplo.com");
        using (var client = PatientClient(before.KeycloakId, before.Email))
        {
            (await client.DeleteAsync("/api/v1/patients/me?confirmedActivePilotAcknowledged=true")).IsSuccessStatusCode.Should().BeTrue();
        }

        (await RunAsync("reset-qa", "CP066")).Should().Be(CommandRunner.Success);

        var after = await UserAsync("paciente.eliminar@ejemplo.com");
        after.Id.Should().NotBe(before.Id);
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.PatientProfiles.CountAsync(profile => profile.UserId == after.Id)).Should().Be(1);
        (await db.Meals.CountAsync(meal => meal.PatientId == after.Id)).Should().Be(1);
        (await db.Symptoms.CountAsync(symptom => symptom.PatientId == after.Id)).Should().Be(1);
        Factory.KeycloakClient.DeletedUsers.Should().Contain(before.KeycloakId);
    }

    [SkippableFact]
    public async Task ResetQa_LinkedPatient_RetiredCopyDisappearsFromEveryNutritionistList()
    {
        SkipIfUnavailable();
        var before = await UserAsync("paciente.entrega@ejemplo.com");

        (await RunAsync("reset-qa", "CP036")).Should().Be(CommandRunner.Success);

        var after = await UserAsync("paciente.entrega@ejemplo.com");
        var nutritionist = await UserAsync("nutricionista.demo@ejemplo.com");
        using var client = NutritionistClient(nutritionist.KeycloakId, nutritionist.Email);
        var patients = await client.GetStringAsync("/api/v1/nutritionists/me/patients");
        var pending = await client.GetStringAsync("/api/v1/recommendations/pending-review?page=1&pageSize=100");
        patients.Should().Contain(after.Id.ToString()).And.NotContain(before.Id.ToString());
        pending.Should().Contain(after.Id.ToString()).And.NotContain(before.Id.ToString());
        (await UserAsync("nutricionista.demo@ejemplo.com")).Id.Should().Be(nutritionist.Id);
    }

    [SkippableFact]
    public async Task ResetQa_LockedAccountInCp015_IsRecreatedWithoutLock()
    {
        SkipIfUnavailable();
        var before = await UserAsync("paciente.bloqueo@ejemplo.com");
        Factory.KeycloakClient.LockUser(before.KeycloakId, DateTime.UtcNow.AddMinutes(15));

        (await RunAsync("reset-qa", "CP015")).Should().Be(CommandRunner.Success);

        var after = await UserAsync("paciente.bloqueo@ejemplo.com");
        after.Id.Should().NotBe(before.Id);
        after.LockedUntil.Should().BeNull();
        (await Factory.KeycloakClient.GetBruteForceStatusAsync(after.KeycloakId)).Should().BeNull();
    }

    [SkippableFact]
    public async Task ResetQa_SharedAccount_IsUnlockedAndKeepsItsIdentity()
    {
        SkipIfUnavailable();
        var before = await UserAsync("paciente.demo@ejemplo.com");
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var tracked = await db.Users.SingleAsync(user => user.Id == before.Id);
            tracked.Lock(DateTime.UtcNow.AddMinutes(15));
            await db.SaveChangesAsync();
        }

        (await RunAsync("reset-qa", "paciente.demo@ejemplo.com")).Should().Be(CommandRunner.Success);

        var after = await UserAsync("paciente.demo@ejemplo.com");
        after.Id.Should().Be(before.Id);
        after.LockedUntil.Should().BeNull();
        Factory.QaKeycloak.BruteForceCleared.Should().Contain(before.KeycloakId);
    }

    [SkippableFact]
    public async Task ResetQa_AccountRegisteredDuringCp001_IsRetired()
    {
        SkipIfUnavailable();
        var registered = await RegisterPatientAsync("registro.cp001@ejemplo.com");

        (await RunAsync("reset-qa", "CP001")).Should().Be(CommandRunner.Success);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Users.AnyAsync(user => user.Email == "registro.cp001@ejemplo.com")).Should().BeFalse();
        (await db.Users.SingleAsync(user => user.Id == registered)).Status.Should().Be(UserStatus.Inactive);
        (await Factory.KeycloakClient.FindByEmailAsync("registro.cp001@ejemplo.com")).Should().BeNull();
    }

    [SkippableFact]
    public async Task ResetQa_PendingActivationCase_ResendsTheActivationLinkAndStaysPending()
    {
        SkipIfUnavailable();
        var sentBefore = Factory.KeycloakClient.UpdatePasswordEmailsSent.Count;

        (await RunAsync("reset-qa", "CP005")).Should().Be(CommandRunner.Success);

        var after = await UserAsync("nutricionista.nuevo@ejemplo.com");
        after.Status.Should().Be(UserStatus.PendingActivation);
        Factory.KeycloakClient.UpdatePasswordEmailsSent.Should().HaveCount(sentBefore + 1);
        Factory.KeycloakClient.UpdatePasswordEmailsSent[^1].Should().Be(after.KeycloakId);
    }

    [SkippableFact]
    public async Task ResetQa_ClockCase_ReanchorsToTheResetInstant()
    {
        SkipIfUnavailable();
        Factory.Clock.Advance(TimeSpan.FromDays(3));
        var anchor = Factory.Clock.GetUtcNow().UtcDateTime;

        (await RunAsync("reset-qa", "CP031")).Should().Be(CommandRunner.Success);

        var patient = await UserAsync("paciente.recordatorio1@ejemplo.com");
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.IbsSssSchedules.SingleAsync(schedule => schedule.PatientId == patient.Id)).DueDate
            .Should().BeCloseTo(anchor.AddHours(-48), TimeSpan.FromSeconds(5));
    }

    [SkippableFact]
    public async Task ResetQa_UnknownTarget_FailsWithoutTouchingAnything()
    {
        SkipIfUnavailable();
        var before = await UserAsync("paciente.reset3@ejemplo.com");

        var code = await RunCapturingAsync("reset-qa", "CP082", "CP999");

        code.Should().Be(CommandRunner.Failure);
        CapturedLog.Should().Contain(line => line.Contains("CP999"));
        (await UserAsync("paciente.reset3@ejemplo.com")).Id.Should().Be(before.Id);
    }

    private async Task<Guid> RegisterPatientAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var roleId = await db.UserRoles.Where(role => role.RoleName == UserRoles.Patient).Select(role => role.RoleId).FirstAsync();
        var keycloakId = await Factory.KeycloakClient.CreateUserAsync(email, "Registro de prueba", UserRoles.Patient, true);
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId, email, "Registro de prueba", roleId, PatientCode.FromCorrelative(900_101));
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<User> UserAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Users.AsNoTracking().SingleAsync(user => user.Email == email);
    }

    private async Task<User> UserByIdAsync(Guid id)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Users.AsNoTracking().SingleAsync(user => user.Id == id);
    }
}
