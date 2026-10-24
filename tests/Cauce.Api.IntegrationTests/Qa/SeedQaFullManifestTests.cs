using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// <c>seed-qa</c> con el manifiesto publicado completo: datos clínicos, recomendaciones, códigos y agendas en el
/// estado de cada caso, sin efectos secundarios e idempotente. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SeedQaFullManifestTests
    : QaCommandTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    public SeedQaFullManifestTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private DateTime Anchor => Factory.Clock.GetUtcNow().UtcDateTime;

    private async Task SeedFullManifestAsync()
    {
        await SeedConsentDocumentAsync();
        var code = await RunCapturingAsync("seed-qa");
        code.Should().Be(CommandRunner.Success, CauceLog);
    }

    [SkippableFact]
    public async Task SeedQa_PublishedManifest_SeedsEveryAccountWithoutSideEffects()
    {
        SkipIfUnavailable();

        await SeedFullManifestAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Users.CountAsync()).Should().Be(105 + 4);
        (await db.Users.CountAsync(user => user.FcmToken != null)).Should().Be(0);
        (await db.Notifications.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
        Factory.EmailSender.SentEmails.Should().BeEmpty();
        var pending = await db.Users.Where(user => user.Status == UserStatus.PendingActivation).Select(user => user.KeycloakId).ToListAsync();
        pending.Should().HaveCount(3 + 1);
        Factory.KeycloakClient.UpdatePasswordEmailsSent.Should().BeEquivalentTo(pending);

        foreach (var email in new[] { "paciente.revision@ejemplo.com", "paciente.rechazo@ejemplo.com", "paciente.ajeno@ejemplo.com" })
        {
            var userId = await UserIdAsync(db, email);
            (await db.Notifications.CountAsync(notification => notification.UserId == userId)).Should().Be(0, email);
        }
    }

    [SkippableFact]
    public async Task SeedQa_PublishedManifest_LeavesRecommendationsInTheirManifestState()
    {
        SkipIfUnavailable();

        await SeedFullManifestAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var filter = await RecommendationsOfAsync(db, "paciente.filtro@ejemplo.com");
        filter.Select(recommendation => recommendation.Status).Should().BeEquivalentTo(new[]
        {
            RecommendationStatus.Approved, RecommendationStatus.Delivered, RecommendationStatus.FeedbackReceived,
            RecommendationStatus.PendingReview, RecommendationStatus.Rejected, RecommendationStatus.Expired
        });
        filter.Single(recommendation => recommendation.Status == RecommendationStatus.FeedbackReceived).Source.Should().Be(RecommendationSource.Manual);
        filter.Single(recommendation => recommendation.Status == RecommendationStatus.FeedbackReceived).Title.Should().Be("Hidratación entre comidas");

        var pendingReview = filter.Single(recommendation => recommendation.Status == RecommendationStatus.PendingReview);
        pendingReview.ExpiresAt.Should().BeCloseTo(Anchor.AddDays(10), TimeSpan.FromSeconds(5));
        var approved = filter.Single(recommendation => recommendation.Status == RecommendationStatus.Approved);
        approved.IsExpired(Anchor.AddDays(7)).Should().BeFalse();
        approved.IsVisibleToPatient().Should().BeTrue();

        var reviewQueue = await RecommendationsOfAsync(db, "paciente.revision@ejemplo.com");
        reviewQueue.Should().ContainSingle().Which.Status.Should().Be(RecommendationStatus.PendingReview);
        reviewQueue[0].Items.Should().ContainSingle().Which.ActionType.Should().Be(ActionType.Reduce);

        var explained = await RecommendationsOfAsync(db, "paciente.explic@ejemplo.com");
        explained.Should().HaveCount(3).And.OnlyContain(recommendation => recommendation.ExplanationSource == ExplanationSource.LlmGenerated);
        explained.Select(recommendation => recommendation.ConfidenceScore.Value).Should().BeEquivalentTo(new[] { 0.85m, 0.60m, 0.30m });

        var fallback = (await RecommendationsOfAsync(db, "paciente.respaldo@ejemplo.com")).Single();
        fallback.ExplanationSource.Should().Be(ExplanationSource.Fallback);
        fallback.AiExplanation.Should().Contain("reducir 2 alimento(s)");

        var archivable = await RecommendationsOfAsync(db, "paciente.vencida@ejemplo.com");
        archivable.Should().OnlyContain(recommendation => recommendation.Source == RecommendationSource.Manual);
        archivable.Select(recommendation => recommendation.ValidUntil!.Value.Date)
            .Should().BeEquivalentTo(new[] { Anchor.AddDays(-2).Date, Anchor.AddDays(30).Date });

        var supported = await PatientIdAsync(db, "paciente.bloques@ejemplo.com");
        (await db.Symptoms.CountAsync(symptom => symptom.PatientId == supported && symptom.SymptomType == SymptomType.AbdominalPain)).Should().Be(4);
    }

    [SkippableFact]
    public async Task SeedQa_PublishedManifest_SeedsClockBoundDataFromTheAnchor()
    {
        SkipIfUnavailable();

        await SeedFullManifestAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var reminder = await PatientIdAsync(db, "paciente.recordatorio1@ejemplo.com");
        (await db.IbsSssSchedules.SingleAsync(schedule => schedule.PatientId == reminder)).DueDate
            .Should().BeCloseTo(Anchor.AddHours(-48), TimeSpan.FromSeconds(5));

        var evolution = await PatientIdAsync(db, "paciente.evol@ejemplo.com");
        var mealDays = await db.Meals.Where(meal => meal.PatientId == evolution).Select(meal => meal.ConsumedAt).ToListAsync();
        mealDays.Select(day => day.Date).Distinct().Should().HaveCount(10);
        mealDays.Should().OnlyContain(day => day > Anchor.AddDays(-14) && day <= Anchor);
        var periodic = await db.IbsSssAssessments.Where(assessment => assessment.PatientId == evolution && assessment.AssessmentType == AssessmentType.Periodic)
            .OrderBy(assessment => assessment.CycleNumber).Select(assessment => assessment.TotalScore).ToListAsync();
        periodic.Should().Equal(280, 245);

        var monthly = await PatientIdAsync(db, "paciente.reporte1@ejemplo.com");
        var fixedDates = await db.IbsSssAssessments.Where(assessment => assessment.PatientId == monthly)
            .Select(assessment => assessment.CompletedAt).ToListAsync();
        fixedDates.Should().BeEquivalentTo(new[]
        {
            new DateTime(2026, 8, 5, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 19, 17, 0, 0, DateTimeKind.Utc)
        });

        var codes = await db.InvitationCodes.Where(code => code.NutritionistId == db.Users.Single(user => user.Email == "nutricionista.codigo2@ejemplo.com").Id)
            .ToListAsync();
        codes.Select(code => code.Status).Should().BeEquivalentTo(new[]
        {
            InvitationStatus.Expired, InvitationStatus.Expired, InvitationStatus.Used, InvitationStatus.Used, InvitationStatus.Active
        });
        var demo = await PatientIdAsync(db, "paciente.demo@ejemplo.com");
        codes.Where(code => code.Status == InvitationStatus.Used).Should().OnlyContain(code => code.UsedByPatientId == demo);
        (await db.NutritionistPatients.AnyAsync(link => link.PatientId == demo)).Should().BeFalse();
    }

    [SkippableFact]
    public async Task SeedQa_PublishedManifestTwice_AddsNothingTheSecondTime()
    {
        SkipIfUnavailable();
        await SeedFullManifestAsync();
        var first = await CountsAsync();

        (await RunAsync("seed-qa")).Should().Be(CommandRunner.Success);

        (await CountsAsync()).Should().BeEquivalentTo(first);
    }

    private async Task<object> CountsAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return new
        {
            Users = await db.Users.CountAsync(),
            Consents = await db.ConsentRecords.CountAsync(),
            Profiles = await db.PatientProfiles.CountAsync(),
            Links = await db.NutritionistPatients.CountAsync(),
            Meals = await db.Meals.CountAsync(),
            Symptoms = await db.Symptoms.CountAsync(),
            Assessments = await db.IbsSssAssessments.CountAsync(),
            Schedules = await db.IbsSssSchedules.CountAsync(),
            Recommendations = await db.Recommendations.CountAsync(),
            Codes = await db.InvitationCodes.CountAsync(),
            CustomFoods = await db.CustomFoods.CountAsync()
        };
    }

    private static async Task<Guid> UserIdAsync(CauceDbContext db, string email) =>
        await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();

    private static Task<Guid> PatientIdAsync(CauceDbContext db, string email) => UserIdAsync(db, email);

    private static async Task<List<Cauce.Domain.Recommendations.Recommendation>> RecommendationsOfAsync(CauceDbContext db, string email)
    {
        var patientId = await UserIdAsync(db, email);
        return await db.Recommendations.AsNoTracking().Include(recommendation => recommendation.Items)
            .Where(recommendation => recommendation.PatientId == patientId).ToListAsync();
    }
}
