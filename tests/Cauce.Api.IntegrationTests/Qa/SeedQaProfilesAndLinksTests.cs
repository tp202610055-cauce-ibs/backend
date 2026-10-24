using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Domain.Patients.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Pruebas de <c>seed-qa</c> sobre perfiles, alergias y vínculos. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SeedQaProfilesAndLinksTests
    : QaCommandTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string Seeds = """
        [
          {"t":"profile","patient":"paciente.qa@ejemplo.com","complete":true,"weightKg":70,"heightCm":170,"subtype":"SII-E","allergies":["Lactosa"]},
          {"t":"link","patient":"paciente.qa@ejemplo.com","nutritionist":"nutricionista.qa@ejemplo.com","since":{"days":20}},
          {"t":"profile","patient":"paciente.qa.nuevo@ejemplo.com","complete":false},
          {"t":"profile","patient":"paciente.qa.base@ejemplo.com","complete":true,"allergies":[]},
          {"t":"link","patient":"paciente.qa.base@ejemplo.com","nutritionist":"nutricionista.qa@ejemplo.com"}
        ]
        """;

    public SeedQaProfilesAndLinksTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private static string Manifest() => new QaManifestBuilder()
        .Patient("paciente.qa@ejemplo.com")
        .Patient("paciente.qa.nuevo@ejemplo.com")
        .Patient("paciente.qa.base@ejemplo.com")
        .Nutritionist("nutricionista.qa@ejemplo.com")
        .Case("CP901", ["paciente.qa@ejemplo.com", "paciente.qa.nuevo@ejemplo.com", "paciente.qa.base@ejemplo.com", "nutricionista.qa@ejemplo.com"], Seeds)
        .Build();

    [SkippableFact]
    public async Task SeedQa_ProfilesAndLinks_SeedsWhatTheManifestSays()
    {
        SkipIfUnavailable();
        await SeedConsentDocumentAsync();

        var code = await RunCapturingAsync("seed-qa", "--manifest", WriteManifest(Manifest()));

        code.Should().Be(CommandRunner.Success, CauceLog);
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var patient = await db.Users.AsNoTracking().SingleAsync(user => user.Email == "paciente.qa@ejemplo.com");
        var profile = await db.PatientProfiles.AsNoTracking().SingleAsync(candidate => candidate.UserId == patient.Id);
        profile.OnboardingCompleted.Should().BeTrue();
        profile.WeightKg.Should().Be(70m);
        profile.IbsSubtype.Should().Be(IbsSubtype.IbsC);
        var allergies = await db.PatientAllergies.AsNoTracking().Where(allergy => allergy.PatientId == patient.Id)
            .Join(db.Allergies, declared => declared.AllergyId, allergy => allergy.Id, (_, allergy) => allergy.Name)
            .ToListAsync();
        allergies.Should().Equal("Lactosa");

        var newcomer = await db.Users.AsNoTracking().SingleAsync(user => user.Email == "paciente.qa.nuevo@ejemplo.com");
        (await db.PatientProfiles.AnyAsync(candidate => candidate.UserId == newcomer.Id)).Should().BeFalse();
        var baseline = await db.Users.AsNoTracking().SingleAsync(user => user.Email == "paciente.qa.base@ejemplo.com");
        (await db.PatientAllergies.AnyAsync(allergy => allergy.PatientId == baseline.Id)).Should().BeFalse();

        var link = await db.NutritionistPatients.AsNoTracking().SingleAsync(candidate => candidate.PatientId == patient.Id);
        link.Status.Should().Be(AssignmentStatus.Active);
        link.AssignedAt.Should().BeCloseTo(Factory.Clock.GetUtcNow().UtcDateTime.AddDays(-20), TimeSpan.FromSeconds(5));
        (await db.Notifications.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [SkippableFact]
    public async Task SeedQa_LinkedPatients_AppearInTheNutritionistPanel()
    {
        SkipIfUnavailable();
        await SeedConsentDocumentAsync();
        (await RunAsync("seed-qa", "--manifest", WriteManifest(Manifest()))).Should().Be(CommandRunner.Success);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var nutritionist = await db.Users.AsNoTracking().SingleAsync(user => user.Email == "nutricionista.qa@ejemplo.com");
        var patientIds = await db.Users.AsNoTracking()
            .Where(user => user.Email == "paciente.qa@ejemplo.com" || user.Email == "paciente.qa.base@ejemplo.com")
            .Select(user => user.Id)
            .ToListAsync();

        using var client = NutritionistClient(nutritionist.KeycloakId, nutritionist.Email);
        var body = await client.GetStringAsync("/api/v1/nutritionists/me/patients");

        foreach (var patientId in patientIds)
        {
            body.Should().Contain(patientId.ToString());
        }
    }

    [SkippableFact]
    public async Task SeedQa_RunTwice_DoesNotDuplicateProfilesAllergiesOrLinks()
    {
        SkipIfUnavailable();
        await SeedConsentDocumentAsync();
        var manifest = WriteManifest(Manifest());

        (await RunAsync("seed-qa", "--manifest", manifest)).Should().Be(CommandRunner.Success);
        (await RunAsync("seed-qa", "--manifest", manifest)).Should().Be(CommandRunner.Success);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.PatientProfiles.CountAsync()).Should().Be(2);
        (await db.PatientAllergies.CountAsync()).Should().Be(1);
        (await db.NutritionistPatients.CountAsync()).Should().Be(2);
    }
}
