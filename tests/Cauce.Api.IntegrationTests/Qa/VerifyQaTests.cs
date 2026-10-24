using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Domain.Identity;
using Cauce.Domain.Recommendations.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Pruebas de <c>verify-qa</c>: todo en OK tras sembrar sin modificar nada, y FALLA con motivo cuando un caso se
/// consumió, cuando existe una cuenta prohibida o cuando el reloj quedó atrás. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class VerifyQaTests
    : QaCommandTestBase, IAsyncLifetime, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    public VerifyQaTests(PostgresFixture postgres, RedisFixture redis)
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
    public async Task VerifyQa_AfterSeeding_ReportsEveryCaseOkAndChangesNothing()
    {
        SkipIfUnavailable();
        var before = await StateAsync();
        var keycloakWrites = KeycloakWrites();

        var code = await RunCapturingAsync("verify-qa", "--all");

        code.Should().Be(CommandRunner.Success, CauceLog);
        CapturedLog.Count(line => line.StartsWith("Cauce.Commands: CP", StringComparison.Ordinal) && line.EndsWith(" OK", StringComparison.Ordinal))
            .Should().Be(90);
        CapturedLog.Should().Contain(line => line.Contains("verify-qa: 90 OK, 0 FALLA de 90 caso(s)."));
        (await StateAsync()).Should().BeEquivalentTo(before);
        KeycloakWrites().Should().Be(keycloakWrites);
    }

    [SkippableFact]
    public async Task VerifyQa_ConsumedCase_FailsWithItsReasonUntilItIsReset()
    {
        SkipIfUnavailable();
        await ApproveTheOnlyRecommendationOfAsync("paciente.revision@ejemplo.com");

        var code = await RunCapturingAsync("verify-qa", "CP037");

        code.Should().Be(CommandRunner.Failure);
        CapturedLog.Should().Contain(line => line.Contains("CP037 FALLA") && line.Contains("PendingReview"));

        (await RunAsync("reset-qa", "CP037")).Should().Be(CommandRunner.Success);
        (await RunAsync("verify-qa", "CP037")).Should().Be(CommandRunner.Success);
    }

    [SkippableFact]
    public async Task VerifyQa_ForbiddenAccountPresent_Fails()
    {
        SkipIfUnavailable();
        await Factory.KeycloakClient.CreateUserAsync("registro.cp001@ejemplo.com", "Registro", UserRoles.Patient, true);

        var code = await RunCapturingAsync("verify-qa", "CP001");

        code.Should().Be(CommandRunner.Failure);
        CapturedLog.Should().Contain(line => line.Contains("CP001 FALLA") && line.Contains("registro.cp001@ejemplo.com existe"));
    }

    [SkippableFact]
    public async Task VerifyQa_HoursClockLeftBehind_FailsOnlyTheClockCases()
    {
        SkipIfUnavailable();
        Factory.Clock.Advance(TimeSpan.FromHours(3));

        (await RunCapturingAsync("verify-qa", "CP024", "CP002")).Should().Be(CommandRunner.Failure);

        CapturedLog.Should().Contain(line => line.Contains("CP024 FALLA") && line.Contains("reset-qa CP024"));
        CapturedLog.Should().Contain(line => line.EndsWith("CP002 OK", StringComparison.Ordinal));
    }

    [SkippableFact]
    public async Task VerifyQa_WithoutQaMode_ExitsWithTwo()
    {
        SkipIfUnavailable();
        var settings = new Dictionary<string, string?>(QaVariables) { ["QA_MODE"] = null };
        await using var factory = new CustomWebApplicationFactory(Postgres.ConnectionString, Redis.ConnectionString, additionalSettings: settings);

        (await Cauce.Api.Commands.CommandRunner.RunAsync(factory.Services, ["verify-qa", "--all"])).Should().Be(CommandRunner.Refused);
    }

    private async Task ApproveTheOnlyRecommendationOfAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var patient = await db.Users.SingleAsync(user => user.Email == email);
        var nutritionist = await db.Users.SingleAsync(user => user.Email == "nutricionista.demo@ejemplo.com");
        var recommendation = await db.Recommendations.SingleAsync(candidate => candidate.PatientId == patient.Id);
        recommendation.Status.Should().Be(RecommendationStatus.PendingReview);
        recommendation.Approve(nutritionist.Id, "Aprobada durante la prueba.", DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private int KeycloakWrites() =>
        Factory.KeycloakClient.PasswordResets.Count + Factory.KeycloakClient.DeletedUsers.Count
        + Factory.KeycloakClient.UpdatePasswordEmailsSent.Count + Factory.QaKeycloak.EnabledByKeycloakId.Count
        + Factory.QaKeycloak.BruteForceCleared.Count;

    private async Task<object> StateAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return new
        {
            Users = await db.Users.AsNoTracking().OrderBy(user => user.Id).Select(user => new { user.Id, user.Status, user.UpdatedAt }).ToListAsync(),
            Recommendations = await db.Recommendations.AsNoTracking().OrderBy(recommendation => recommendation.Id)
                .Select(recommendation => new { recommendation.Id, recommendation.Status }).ToListAsync(),
            Audit = await db.AuditLogs.CountAsync(),
            Codes = await db.InvitationCodes.AsNoTracking().Select(code => code.Status).ToListAsync()
        };
    }
}
