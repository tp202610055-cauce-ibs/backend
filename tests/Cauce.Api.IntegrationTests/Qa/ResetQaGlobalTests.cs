using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Rearme global (<c>reset-qa</c> con los 90 casos) sobre una base sin sembrar. Requiere Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ResetQaGlobalTests
    : QaCommandTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    public ResetQaGlobalTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task ResetQa_EveryCaseOnAnEmptyDatabase_SeedsEverythingAndVerifies()
    {
        SkipIfUnavailable();
        await SeedConsentDocumentAsync();
        await SeedGlossaryAsync();

        var code = await RunCapturingAsync(["reset-qa", .. SeedQaPreconditionsTests.AllCases]);

        code.Should().Be(CommandRunner.Success, CauceLog);
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            // Rearmar casos siembra solo sus cuentas; las de humo las crea seed-qa.
            (await db.Users.CountAsync()).Should().Be(105);
        }

        (await RunCapturingAsync("verify-qa", "--all")).Should().Be(CommandRunner.Success, CauceLog);
    }
}
