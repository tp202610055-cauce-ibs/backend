using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Reports;
using Cauce.Application.Patients.Services;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Qa.Manifest;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Precondiciones de la hoja v4 sobre los datos que deja <c>seed-qa</c> con el manifiesto publicado: panel de CP049,
/// reporte de CP059, sustitutos de CP036, glosario de CP068, historiales de CP040, CP042 y CP045, rearme global y las
/// comprobaciones de <c>verify-qa</c> que fallan si faltan. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SeedQaPreconditionsTests
    : QaCommandTestBase, IAsyncLifetime, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Los 90 casos del manifiesto, como los pasa el rearme global.
    /// </summary>
    public static readonly string[] AllCases = Enumerable.Range(1, 90).Select(number => $"CP{number:000}").ToArray();

    public SeedQaPreconditionsTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private DateTime Anchor { get; set; }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Available)
        {
            await SeedConsentDocumentAsync();
            await SeedGlossaryAsync();
            Anchor = Factory.Clock.GetUtcNow().UtcDateTime;
            (await RunCapturingAsync("seed-qa")).Should().Be(CommandRunner.Success, CauceLog);
        }
    }

    [SkippableFact]
    public async Task SeedQa_Cp049_PanelRanksHighThenMediumThenTheRest()
    {
        SkipIfUnavailable();

        var atSeed = await PanelAsync(Anchor);
        var nearDeadline = await PanelAsync(Anchor.AddHours(3));

        foreach (var panel in new[] { atSeed, nearDeadline })
        {
            panel.Take(2).Select(row => row.Email).Should().BeEquivalentTo("paciente.panel1@ejemplo.com", "paciente.panel2@ejemplo.com");
            panel.Take(2).Should().OnlyContain(row => row.Priority == PriorityLevel.High);
            panel[2].Email.Should().Be("paciente.panel5@ejemplo.com");
            panel[2].Priority.Should().Be(PriorityLevel.Medium);
            panel.Skip(3).Select(row => row.Email).Should().BeEquivalentTo(
                "paciente.panel3@ejemplo.com", "paciente.panel4@ejemplo.com", "paciente.panel6@ejemplo.com");
            panel.Skip(3).Should().OnlyContain(row => row.Priority == PriorityLevel.Low);
        }
    }

    [SkippableFact]
    public async Task SeedQa_Cp059_JulyHasNoDataAndJulyToTodayDoes()
    {
        SkipIfUnavailable();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var reader = scope.ServiceProvider.GetRequiredService<IClinicalReportDataReader>();
        var patientId = await UserIdAsync(db, "paciente.reporte2@ejemplo.com");
        var today = new QaClock(Anchor).LimaToday;

        (await reader.HasDataInPeriodAsync(patientId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31))).Should().BeFalse();
        (await reader.HasDataInPeriodAsync(patientId, new DateOnly(2026, 7, 1), today)).Should().BeTrue();
        var limit = QaClock.AtLimaTime(new DateOnly(2026, 9, 16), TimeOnly.MinValue);
        (await db.Meals.AnyAsync(meal => meal.PatientId == patientId && meal.ConsumedAt < limit)).Should().BeFalse();
        (await db.Symptoms.AnyAsync(symptom => symptom.PatientId == patientId && symptom.OccurredAt < limit)).Should().BeFalse();
    }

    [SkippableFact]
    public async Task SeedQa_Cp036_PendingRecommendationProposesSubstitutesWithFodmapReason()
    {
        SkipIfUnavailable();
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var patientId = await UserIdAsync(db, "paciente.entrega@ejemplo.com");

        var recommendation = await db.Recommendations.AsNoTracking().Include(candidate => candidate.Items)
            .SingleAsync(candidate => candidate.PatientId == patientId);

        recommendation.Status.Should().Be(RecommendationStatus.PendingReview);
        var names = await db.FoodItems.ToDictionaryAsync(food => food.Id, food => food.Name);
        recommendation.Items.Where(item => item.ActionType == ActionType.Substitute)
            .Select(item => $"{names[item.FoodId]} por {names[item.SubstituteFoodId!.Value]}")
            .Should().BeEquivalentTo("Cebolla cocida por Zanahoria cocida", "Pan de trigo blanco por Arroz blanco cocido");
        recommendation.Items.Where(item => item.ActionType == ActionType.Substitute)
            .Should().OnlyContain(item => item.Reasoning!.Contains("FODMAP"));
        recommendation.Items.Count(item => item.ActionType == ActionType.Reduce).Should().Be(2);
        recommendation.Items.Count(item => item.ActionType == ActionType.Suggest).Should().Be(1);
    }

    [SkippableFact]
    public async Task SeedQa_Cp068_RealignsBaselineTermToApplicationCategory()
    {
        SkipIfUnavailable();
        await SetCategoryAsync("Línea base", GlossaryCategory.ClinicalIbs);

        (await RunCapturingAsync("seed-qa")).Should().Be(CommandRunner.Success, CauceLog);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var categories = await db.GlossaryTerms.AsNoTracking().ToDictionaryAsync(term => term.Term, term => term.Category);
        categories["Línea base"].Should().Be(GlossaryCategory.System);
        categories["FODMAP"].Should().Be(GlossaryCategory.Nutritional);
        categories["IBS-SSS"].Should().Be(GlossaryCategory.ClinicalIbs);
        categories.Should().ContainKey("Disacáridos").And.NotContainKey("disacaridasas").And.NotContainKey("xyzqw");
    }

    [SkippableFact]
    public async Task SeedQa_SupportingHistories_StayInTheirWindowsForAWeek()
    {
        SkipIfUnavailable();
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var weekLater = Anchor.AddDays(7);

        (await PainSinceAsync(db, "paciente.bloques@ejemplo.com", weekLater.AddDays(-14))).Should().Be(4);
        (await PainSinceAsync(db, "paciente.respaldo@ejemplo.com", weekLater.AddDays(-14))).Should().Be(4);
        (await PainSinceAsync(db, "paciente.explic@ejemplo.com", Anchor.AddDays(-7))).Should().Be(3);
        (await PainSinceAsync(db, "paciente.aprob@ejemplo.com", Anchor.AddDays(-7))).Should().Be(3);
    }

    [SkippableFact]
    public async Task ResetQa_Cp073AndEveryCaseOnASeededDatabase_SucceedsWithoutDuplicatingSharedData()
    {
        SkipIfUnavailable();
        var before = await SharedStateAsync();

        (await RunCapturingAsync("reset-qa", "CP073")).Should().Be(CommandRunner.Success, CauceLog);
        (await SharedStateAsync()).Should().BeEquivalentTo(before);

        (await RunCapturingAsync(["reset-qa", .. AllCases])).Should().Be(CommandRunner.Success, CauceLog);
        (await SharedStateAsync()).Should().BeEquivalentTo(before);
        (await RunCapturingAsync("verify-qa", "--all")).Should().Be(CommandRunner.Success, CauceLog);
    }

    [SkippableFact]
    public async Task VerifyQa_MissingFixedFacts_FailsEachCaseWithItsReason()
    {
        SkipIfUnavailable();
        (await RunAsync("verify-qa", "CP036", "CP040", "CP049", "CP059", "CP068")).Should().Be(CommandRunner.Success);

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var panel1 = await UserIdAsync(db, "paciente.panel1@ejemplo.com");
            await db.Meals.Where(meal => meal.PatientId == panel1).ExecuteDeleteAsync();
            var panel5 = await UserIdAsync(db, "paciente.panel5@ejemplo.com");
            await db.NutritionistPatients.Where(link => link.PatientId == panel5)
                .ExecuteUpdateAsync(setters => setters.SetProperty(link => link.AssignedAt, Anchor));
            var report = await UserIdAsync(db, "paciente.reporte2@ejemplo.com");
            await db.Symptoms.Where(symptom => symptom.PatientId == report).ExecuteDeleteAsync();
            var delivery = await UserIdAsync(db, "paciente.entrega@ejemplo.com");
            await db.Set<Cauce.Domain.Recommendations.RecommendationItem>()
                .Where(item => item.ActionType == ActionType.Substitute
                    && db.Recommendations.Any(recommendation => recommendation.Id == item.RecommendationId && recommendation.PatientId == delivery))
                .ExecuteDeleteAsync();
            var explained = await UserIdAsync(db, "paciente.explic@ejemplo.com");
            await db.Symptoms.Where(symptom => symptom.PatientId == explained).ExecuteDeleteAsync();
        }

        await SetCategoryAsync("Línea base", GlossaryCategory.ClinicalIbs);

        var code = await RunCapturingAsync("verify-qa", "CP036", "CP040", "CP049", "CP059", "CP068");

        code.Should().Be(CommandRunner.Failure);
        CapturedLog.Should().Contain(line => line.Contains("CP049 FALLA")
            && line.Contains("paciente.panel1@ejemplo.com tiene 0 comida(s)")
            && line.Contains("el vínculo de paciente.panel5@ejemplo.com es más reciente"));
        CapturedLog.Should().Contain(line => line.Contains("CP059 FALLA") && line.Contains("síntoma(s) «Dolor abdominal»"));
        CapturedLog.Should().Contain(line => line.Contains("CP036 FALLA") && line.Contains("0 sustituto(s)"));
        CapturedLog.Should().Contain(line => line.Contains("CP040 FALLA") && line.Contains("0 registro(s) de dolor abdominal"));
        CapturedLog.Should().Contain(line => line.Contains("CP068 FALLA") && line.Contains("«Línea base» es ClinicalIbs"));
    }

    private async Task<List<(string Email, PriorityLevel Priority)>> PanelAsync(DateTime utcNow)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<INutritionistPatientRepository>();
        var nutritionistId = await UserIdAsync(db, "nutricionista.panel@ejemplo.com");
        var emails = await db.Users.ToDictionaryAsync(user => user.Id, user => user.Email);

        var rows = await repository.ListAssignedPatientTriageRowsAsync(nutritionistId, utcNow);
        return AssignedPatientTriage.Rank(rows, utcNow)
            .Select(summary => (emails[summary.PatientUserId], summary.PriorityLevel))
            .ToList();
    }

    private async Task SetCategoryAsync(string term, GlossaryCategory category)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var entry = await db.GlossaryTerms.SingleAsync(candidate => candidate.Term == term);
        db.Entry(entry).Property(nameof(GlossaryTerm.Category)).CurrentValue = category;
        await db.SaveChangesAsync();
    }

    private async Task<object> SharedStateAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var shared = new[] { "paciente.ajeno@ejemplo.com", "paciente.demo@ejemplo.com", "nutricionista.demo@ejemplo.com", "nutricionista.error@ejemplo.com" };
        var ids = await db.Users.Where(user => shared.Contains(user.Email)).Select(user => user.Id).ToListAsync();
        return new
        {
            Users = await db.Users.CountAsync(user => shared.Contains(user.Email)),
            Profiles = await db.PatientProfiles.CountAsync(profile => ids.Contains(profile.UserId)),
            Links = await db.NutritionistPatients.CountAsync(link => link.Status == AssignmentStatus.Active && ids.Contains(link.PatientId)),
            Glossary = await db.GlossaryTerms.CountAsync()
        };
    }

    private static async Task<int> PainSinceAsync(CauceDbContext db, string email, DateTime since)
    {
        var patientId = await UserIdAsync(db, email);
        return await db.Symptoms.CountAsync(symptom =>
            symptom.PatientId == patientId && symptom.SymptomType == SymptomType.AbdominalPain && symptom.OccurredAt >= since);
    }

    private static async Task<Guid> UserIdAsync(CauceDbContext db, string email) =>
        await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
}
