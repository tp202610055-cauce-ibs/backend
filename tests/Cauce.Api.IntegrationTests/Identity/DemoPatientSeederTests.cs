using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Identity;
using Cauce.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas del paciente demo: cuenta lista para el Diario, historial corto, idempotencia y apagado. En
/// Testing la cadena de sembrado no corre, así que se invoca a mano.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DemoPatientSeederTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string DemoEmail = "paciente.demo@cauce.local";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public DemoPatientSeederTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task RunDemoSeederAsync(bool enabled = true)
    {
        var options = Options.Create(new DemoPatientOptions
        {
            Enabled = enabled,
            Email = DemoEmail,
            FullName = "Paciente Demo Kaelín",
            Password = "Demo#Prueba2026"
        });

        using var scope = Factory.Services.CreateScope();
        var seeder = ActivatorUtilities.CreateInstance<DemoPatientSeeder>(scope.ServiceProvider, options);
        await seeder.SeedAsync();
    }

    [SkippableFact]
    public async Task SeedAsync_CreatesAReadyPatientWithShortHistory()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        await RunDemoSeederAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Email == DemoEmail);
        user.EmailVerified.Should().BeTrue();
        user.IsInActivePilot.Should().BeTrue();
        Factory.KeycloakClient.PasswordResets.Should().Contain(user.KeycloakId);

        var profile = await db.PatientProfiles.AsNoTracking().SingleAsync(candidate => candidate.UserId == user.Id);
        profile.OnboardingCompleted.Should().BeTrue();
        profile.IbsSubtype.Should().Be(IbsSubtype.IbsD);

        var consent = await db.ConsentRecords.AsNoTracking().SingleAsync(candidate => candidate.UserId == user.Id);
        consent.DocumentVersion.Should().Be("1.0");
        var consentService = Factory.Services.GetRequiredService<IConsentService>();
        consent.DocumentVersion.Should().Be(consentService.GetCurrentVersion());
        consent.ConsentTextHash.Should().Be(consentService.GetCurrentTextHash());

        (await db.IbsSssAssessments.CountAsync(candidate => candidate.PatientId == user.Id && candidate.AssessmentType == AssessmentType.Baseline))
            .Should().Be(1);
        (await db.NutritionistPatients.CountAsync(link => link.PatientId == user.Id && link.NutritionistId == nutritionist.Id))
            .Should().Be(1);

        (await db.Meals.CountAsync(meal => meal.PatientId == user.Id)).Should().Be(5);
        (await db.Symptoms.CountAsync(symptom => symptom.PatientId == user.Id)).Should().Be(3);
        (await db.Symptoms.CountAsync(symptom => symptom.PatientId == user.Id && symptom.HasMealAssociation)).Should().Be(1);
        (await db.CustomFoods.CountAsync(food => food.PatientId == user.Id)).Should().Be(1);
        (await db.ClinicalNotes.CountAsync(note => note.PatientId == user.Id)).Should().Be(1);
    }

    [SkippableFact]
    public async Task SeedAsync_RunTwice_IsIdempotent()
    {
        SkipIfUnavailable();

        await RunDemoSeederAsync();
        await RunDemoSeederAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Users.CountAsync(candidate => candidate.Email == DemoEmail)).Should().Be(1);
        var userId = await db.Users.Where(candidate => candidate.Email == DemoEmail).Select(candidate => candidate.Id).SingleAsync();
        (await db.Meals.CountAsync(meal => meal.PatientId == userId)).Should().Be(5);
        (await db.ConsentRecords.CountAsync(record => record.UserId == userId)).Should().Be(1);
    }

    [SkippableFact]
    public async Task SeedAsync_Disabled_DoesNothing()
    {
        SkipIfUnavailable();

        await RunDemoSeederAsync(enabled: false);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Users.AnyAsync(candidate => candidate.Email == DemoEmail)).Should().BeFalse();
    }
}
