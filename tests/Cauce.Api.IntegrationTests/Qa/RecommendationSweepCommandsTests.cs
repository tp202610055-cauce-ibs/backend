using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Api.IntegrationTests.Recommendations.Support;
using Cauce.Api.Workers;
using FluentAssertions;
using static Cauce.Api.IntegrationTests.Recommendations.Support.RecommendationSweepDataset;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Pruebas de <c>archive-recommendations</c> y <c>expire-recommendations</c>: sobre los mismos datos dejan el mismo
/// estado que el worker, informan cuántas afectaron y exigen <c>QA_MODE=1</c>. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RecommendationSweepCommandsTests
    : QaCommandTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    public RecommendationSweepCommandsTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task ArchiveRecommendations_SameDataAsWorker_LeavesTheSameStateAndReportsTheCount()
    {
        SkipIfUnavailable();
        var byWorker = await SweepFreshDatasetAsync(async () =>
        {
            var (scope, db) = CreateDbScope();
            using (scope)
            {
                await RecommendationArchivalWorker.ArchiveDueAsync(db, DateTime.UtcNow, CancellationToken.None);
            }
        });

        var byCommand = await SweepFreshDatasetAsync(async () =>
            (await RunCapturingAsync("archive-recommendations")).Should().Be(CommandRunner.Success, CauceLog));

        byCommand.Should().BeEquivalentTo(byWorker);
        CapturedLog.Should().Contain("Cauce.Commands: archive-recommendations: 1 recomendación(es) archivada(s) por vigencia vencida.");
    }

    [SkippableFact]
    public async Task ExpireRecommendations_SameDataAsWorker_LeavesTheSameStateAndReportsTheCount()
    {
        SkipIfUnavailable();
        var byWorker = await SweepFreshDatasetAsync(() => ExpirationWorkerPass.RunAsync(Factory.Services));

        var byCommand = await SweepFreshDatasetAsync(async () =>
            (await RunCapturingAsync("expire-recommendations")).Should().Be(CommandRunner.Success, CauceLog));

        byCommand.Should().BeEquivalentTo(byWorker);
        CapturedLog.Should().Contain("Cauce.Commands: expire-recommendations: 3 recomendación(es) pasada(s) a vencida.");
    }

    [SkippableTheory]
    [InlineData("archive-recommendations")]
    [InlineData("expire-recommendations")]
    public async Task SweepCommand_WithoutQaMode_ExitsWithTwoAndChangesNothing(string command)
    {
        SkipIfUnavailable();
        var ids = await SeedDatasetAsync();
        var before = await SnapshotAsync(ids);
        var settings = new Dictionary<string, string?>(QaVariables) { ["QA_MODE"] = null };
        await using var factory = new CustomWebApplicationFactory(Postgres.ConnectionString, Redis.ConnectionString, additionalSettings: settings);

        (await RunCapturingAsync(factory, command)).Should().Be(CommandRunner.Refused);

        (await SnapshotAsync(ids)).Should().BeEquivalentTo(before);
    }

    [SkippableFact]
    public async Task SweepCommand_WithArguments_ExitsWithTwo()
    {
        SkipIfUnavailable();

        (await RunAsync("archive-recommendations", "CP075")).Should().Be(CommandRunner.Refused);
    }

    // Deja la base sin datos transaccionales, siembra el juego y devuelve su estado tras el barrido.
    private async Task<IReadOnlyDictionary<string, SweepState>> SweepFreshDatasetAsync(Func<Task> sweep)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            await DatabaseReset.ResetTransactionalDataAsync(db);
        }

        var ids = await SeedDatasetAsync();
        await sweep();
        return await SnapshotAsync(ids);
    }

    private async Task<IReadOnlyDictionary<string, Guid>> SeedDatasetAsync()
    {
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            return await SeedAsync(db, patient.Id, nutritionist.Id, DateTime.UtcNow);
        }
    }

    private async Task<IReadOnlyDictionary<string, SweepState>> SnapshotAsync(IReadOnlyDictionary<string, Guid> ids)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            return await RecommendationSweepDataset.SnapshotAsync(db, ids);
        }
    }
}
