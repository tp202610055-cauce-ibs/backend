using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Pruebas de <c>seed-qa</c> sobre las cuentas: estados, idempotencia, guardas, efectos secundarios y log sin
/// contraseñas. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SeedQaAccountsTests
    : QaCommandTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    public SeedQaAccountsTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private static string AccountsManifest() => new QaManifestBuilder()
        .Patient("paciente.qa@ejemplo.com")
        .Patient("paciente.qa.inactivo@ejemplo.com", "disabled")
        .Nutritionist("nutricionista.qa@ejemplo.com", displayName: "Ana Gómez")
        .Nutritionist("nutricionista.qa.inactivo@ejemplo.com", "disabled")
        .Nutritionist("nutricionista.qa.pendiente@ejemplo.com", "pendingActivation")
        .MustNotExist("registro.qa@ejemplo.com")
        .Case("CP901", ["paciente.qa@ejemplo.com", "nutricionista.qa@ejemplo.com"], mustNotExist: ["registro.qa@ejemplo.com"])
        .Case("CP902", ["paciente.qa.inactivo@ejemplo.com", "nutricionista.qa.inactivo@ejemplo.com", "nutricionista.qa.pendiente@ejemplo.com"])
        .Build();

    [SkippableFact]
    public async Task SeedQa_AccountsManifest_CreatesEveryAccountInItsState()
    {
        SkipIfUnavailable();
        await SeedConsentDocumentAsync();
        var leftover = await SeedPatientWithEmailAsync("registro.qa@ejemplo.com");
        var manifest = WriteManifest(AccountsManifest());

        var code = await RunCapturingAsync("seed-qa", "--manifest", manifest);

        code.Should().Be(CommandRunner.Success, CauceLog);
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var users = await db.Users.AsNoTracking().ToListAsync();

        var patient = users.Single(user => user.Email == "paciente.qa@ejemplo.com");
        patient.Status.Should().Be(UserStatus.Active);
        patient.EmailVerified.Should().BeTrue();
        patient.FcmToken.Should().BeNull();
        (await db.ConsentRecords.CountAsync(consent => consent.UserId == patient.Id && consent.DocumentVersion == "1.0")).Should().Be(1);
        Factory.QaKeycloak.EnabledByKeycloakId[patient.KeycloakId].Should().BeTrue();

        users.Single(user => user.Email == "paciente.qa.inactivo@ejemplo.com").Status.Should().Be(UserStatus.Suspended);
        var nutritionist = users.Single(user => user.Email == "nutricionista.qa@ejemplo.com");
        nutritionist.Status.Should().Be(UserStatus.Active);
        nutritionist.FullName.Should().Be("Ana Gómez");
        var disabled = users.Single(user => user.Email == "nutricionista.qa.inactivo@ejemplo.com");
        disabled.Status.Should().Be(UserStatus.Suspended);
        Factory.QaKeycloak.EnabledByKeycloakId[disabled.KeycloakId].Should().BeFalse();

        var pending = users.Single(user => user.Email == "nutricionista.qa.pendiente@ejemplo.com");
        pending.Status.Should().Be(UserStatus.PendingActivation);
        Factory.KeycloakClient.UpdatePasswordEmailsSent.Should().Contain(pending.KeycloakId);
        Factory.KeycloakClient.PasswordResets.Should().NotContain(pending.KeycloakId);

        users.Should().Contain(user => user.Email == "smoke.nutricionista@ejemplo.com" && user.Status == UserStatus.Active);
        users.Should().Contain(user => user.Email == "smoke.paciente@ejemplo.com" && user.Status == UserStatus.Active);
        users.Should().Contain(user => user.Email == "smoke.nutricionista.inactivo@ejemplo.com" && user.Status == UserStatus.Suspended);
        users.Should().Contain(user => user.Email == "smoke.nutricionista.pendiente@ejemplo.com" && user.Status == UserStatus.PendingActivation);

        users.Should().NotContain(user => user.Email == "registro.qa@ejemplo.com");
        users.Single(user => user.Id == leftover).Status.Should().Be(UserStatus.Inactive);
    }

    [SkippableFact]
    public async Task SeedQa_RunTwice_LeavesTheSameState()
    {
        SkipIfUnavailable();
        await SeedConsentDocumentAsync();
        var manifest = WriteManifest(AccountsManifest());

        (await RunAsync("seed-qa", "--manifest", manifest)).Should().Be(CommandRunner.Success);
        var first = await SnapshotAsync();
        var activationEmails = Factory.KeycloakClient.UpdatePasswordEmailsSent.Count;

        (await RunAsync("seed-qa", "--manifest", manifest)).Should().Be(CommandRunner.Success);

        (await SnapshotAsync()).Should().BeEquivalentTo(first);
        Factory.KeycloakClient.UpdatePasswordEmailsSent.Should().HaveCount(activationEmails);
    }

    [SkippableFact]
    public async Task SeedQa_Accounts_CreatesNoNotificationsOutboxOrEmails()
    {
        SkipIfUnavailable();
        await SeedConsentDocumentAsync();

        (await RunAsync("seed-qa", "--manifest", WriteManifest(AccountsManifest()))).Should().Be(CommandRunner.Success);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Notifications.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
        Factory.EmailSender.SentEmails.Should().BeEmpty();
        var pendingIds = await db.Users.Where(user => user.Status == UserStatus.PendingActivation).Select(user => user.KeycloakId).ToListAsync();
        Factory.KeycloakClient.UpdatePasswordEmailsSent.Should().BeEquivalentTo(pendingIds);
        Factory.KeycloakClient.VerifyEmailsSent.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task SeedQa_LogOfAFullRun_NeverContainsThePasswords()
    {
        SkipIfUnavailable();
        await SeedConsentDocumentAsync();

        var code = await RunCapturingAsync("seed-qa", "--manifest", WriteManifest(AccountsManifest()));

        code.Should().Be(CommandRunner.Success);
        CapturedLog.Should().NotBeEmpty();
        CapturedLog.Should().NotContain(line => line.Contains(PatientPassword) || line.Contains(NutritionistPassword) || line.Contains(SmokePassword));
    }

    [SkippableFact]
    public async Task SeedQa_FoodMissingFromCatalog_FailsBeforeWritingAndListsEverything()
    {
        SkipIfUnavailable();
        await SeedConsentDocumentAsync();
        var manifest = WriteManifest(new QaManifestBuilder()
            .Patient("paciente.qa@ejemplo.com")
            .Case("CP901", ["paciente.qa@ejemplo.com"], """
                [
                  {"t":"meal","patient":"paciente.qa@ejemplo.com","food":"Ajo","ago":{"today":true}},
                  {"t":"profile","patient":"paciente.qa@ejemplo.com","complete":true,"allergies":["Maní"]}
                ]
                """)
            .Build());

        var code = await RunCapturingAsync("seed-qa", "--manifest", manifest);

        code.Should().Be(CommandRunner.Failure);
        CapturedLog.Should().Contain(line => line.Contains("«Ajo»") && line.Contains("«Maní»"));
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Users.CountAsync()).Should().Be(0);
    }

    private async Task<Guid> SeedPatientWithEmailAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var roleId = await db.UserRoles.Where(role => role.RoleName == UserRoles.Patient).Select(role => role.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        await Factory.KeycloakClient.CreateUserAsync(email, "Registro previo", UserRoles.Patient, false);
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId, email, "Registro previo", roleId, PatientCode.FromCorrelative(900_001));
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<object> SnapshotAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var users = await db.Users.AsNoTracking().OrderBy(user => user.Email)
            .Select(user => new { user.Id, user.Email, user.Status, user.KeycloakId, user.PatientCode }).ToListAsync();
        var consents = await db.ConsentRecords.CountAsync();
        return new { users, consents };
    }
}
