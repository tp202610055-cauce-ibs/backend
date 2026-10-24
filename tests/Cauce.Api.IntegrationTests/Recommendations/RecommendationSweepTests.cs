using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Recommendations.Support;
using Cauce.Api.Workers;
using Cauce.Domain.Recommendations.Enums;
using FluentAssertions;
using static Cauce.Api.IntegrationTests.Recommendations.Support.RecommendationSweepDataset;

namespace Cauce.Api.IntegrationTests.Recommendations;

/// <summary>
/// Criterios del archivado por vigencia y de la expiración de recomendaciones, sobre un juego con un caso por cada
/// combinación de estado y fecha. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RecommendationSweepTests : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    public RecommendationSweepTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task ArchiveDue_ArchivesOnlyActiveApprovedRecommendationsPastValidity()
    {
        SkipIfUnavailable();
        var (ids, before) = await SeedDatasetAsync();

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            (await RecommendationArchivalWorker.ArchiveDueAsync(db, DateTime.UtcNow, CancellationToken.None)).Should().Be(1);
        }

        var expected = new Dictionary<string, SweepState>(before)
        {
            ["manual-past"] = new(RecommendationStatus.ManualApproved, false, ArchiveReason.TemporalExpiration)
        };
        (await SnapshotAsync(ids)).Should().BeEquivalentTo(expected);
    }

    [SkippableFact]
    public async Task ExpirationWorker_FirstPass_ExpiresOnlyStaleExpirableRecommendations()
    {
        SkipIfUnavailable();
        var (ids, before) = await SeedDatasetAsync();

        await ExpirationWorkerPass.RunAsync(Factory.Services);

        var expected = new Dictionary<string, SweepState>(before)
        {
            ["generated-past"] = new(RecommendationStatus.Expired, true, null),
            ["pending-past"] = new(RecommendationStatus.Expired, true, null),
            ["approved-past"] = new(RecommendationStatus.Expired, true, null)
        };
        (await SnapshotAsync(ids)).Should().BeEquivalentTo(expected);
    }

    private async Task<(IReadOnlyDictionary<string, Guid> Ids, IReadOnlyDictionary<string, SweepState> Before)> SeedDatasetAsync()
    {
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var ids = await SeedAsync(db, patient.Id, nutritionist.Id, DateTime.UtcNow);
            return (ids, await RecommendationSweepDataset.SnapshotAsync(db, ids));
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
