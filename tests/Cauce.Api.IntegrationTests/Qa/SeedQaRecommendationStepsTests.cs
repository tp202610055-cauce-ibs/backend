using System.Net;
using System.Text.Json;
using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Manifest;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Pasos de aplicación de las recomendaciones del motor que siembra <c>seed-qa</c> (CP042, CP077 y CP086), su lectura
/// por la API y su comprobación en <c>verify-qa</c>. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SeedQaRecommendationStepsTests
    : QaCommandTestBase, IAsyncLifetime, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string BlocksPatient = "paciente.bloques@ejemplo.com";
    private const string FallbackPatient = "paciente.respaldo@ejemplo.com";
    private const string ModifyPatient = "paciente.modexito@ejemplo.com";

    public SeedQaRecommendationStepsTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Available)
        {
            await SeedConsentDocumentAsync();
            await SeedGlossaryAsync();
            (await RunCapturingAsync("seed-qa")).Should().Be(CommandRunner.Success, CauceLog);
        }
    }

    [SkippableFact]
    public async Task SeedQa_Cp042AndCp077_ApprovedEngineRecommendationsCarryThreeSteps()
    {
        SkipIfUnavailable();

        foreach (var (email, source) in new[] { (BlocksPatient, ExplanationSource.LlmGenerated), (FallbackPatient, ExplanationSource.Fallback) })
        {
            var recommendation = await SingleRecommendationAsync(email);

            recommendation.Status.Should().Be(RecommendationStatus.Approved);
            recommendation.Source.Should().Be(RecommendationSource.EngineGenerated);
            recommendation.ExplanationSource.Should().Be(source);
            recommendation.Steps.Should().Equal(QaRecommendationSeed.DefaultSteps);
        }
    }

    [SkippableFact]
    public async Task SeedQa_Cp086_PendingEngineRecommendationCarriesItsStep()
    {
        SkipIfUnavailable();

        var recommendation = await SingleRecommendationAsync(ModifyPatient);

        recommendation.Status.Should().Be(RecommendationStatus.PendingReview);
        recommendation.Source.Should().Be(RecommendationSource.EngineGenerated);
        recommendation.Steps.Should().Equal("Cocinar la zanahoria al vapor");
    }

    [SkippableFact]
    public async Task GetRecommendation_SeededEngineRecommendationAsItsPatient_ReturnsTheSteps()
    {
        SkipIfUnavailable();
        var recommendation = await SingleRecommendationAsync(BlocksPatient);
        var keycloakId = await KeycloakIdAsync(BlocksPatient);

        var response = await PatientClient(keycloakId, BlocksPatient).GetAsync($"/api/v1/recommendations/{recommendation.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("steps").EnumerateArray().Select(step => step.GetString())
            .Should().Equal(QaRecommendationSeed.DefaultSteps);
    }

    [SkippableFact]
    public async Task VerifyQa_StepsMissing_FailsCp042Cp077AndCp086()
    {
        SkipIfUnavailable();
        (await RunCapturingAsync("verify-qa", "CP042", "CP077", "CP086")).Should().Be(CommandRunner.Success, CauceLog);

        foreach (var email in new[] { BlocksPatient, FallbackPatient, ModifyPatient })
        {
            await ClearStepsAsync(email);
        }

        CapturedLog.Clear();
        var code = await RunCapturingAsync("verify-qa", "CP042", "CP077", "CP086");

        code.Should().Be(CommandRunner.Failure);
        CapturedLog.Should().Contain(line => line.Contains("CP042 FALLA") && line.Contains("con sus 3 paso(s)"));
        CapturedLog.Should().Contain(line => line.Contains("CP077 FALLA") && line.Contains("con sus 3 paso(s)"));
        CapturedLog.Should().Contain(line => line.Contains("CP086 FALLA") && line.Contains("con sus 1 paso(s)"));
    }

    private async Task<Recommendation> SingleRecommendationAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var patientId = await UserIdAsync(db, email);
        return await db.Recommendations.AsNoTracking().SingleAsync(candidate => candidate.PatientId == patientId);
    }

    private async Task<string> KeycloakIdAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Users.Where(user => user.Email == email).Select(user => user.KeycloakId).SingleAsync();
    }

    private async Task ClearStepsAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var patientId = await UserIdAsync(db, email);
        var recommendation = await db.Recommendations.SingleAsync(candidate => candidate.PatientId == patientId);
        db.Entry(recommendation).Property(nameof(Recommendation.Steps)).CurrentValue = null;
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> UserIdAsync(CauceDbContext db, string email) =>
        await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
}
