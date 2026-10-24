using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Patients.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Rearme de cuentas exclusivas de nutricionista con <see cref="User.RetireNutritionist"/>: una activación ya usada
/// vuelve a quedar pendiente con un enlace nuevo. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ResetQaNutritionistTests
    : QaCommandTestBase, IAsyncLifetime, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    public ResetQaNutritionistTests(PostgresFixture postgres, RedisFixture redis)
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

    [SkippableTheory]
    [InlineData("CP005", "nutricionista.nuevo@ejemplo.com")]
    [InlineData("CP079", "nutricionista.activar@ejemplo.com")]
    public async Task ResetQa_ActivationAlreadyUsed_LeavesANewPendingAccountWithAFreshLink(string cp, string email)
    {
        SkipIfUnavailable();
        var activated = await ActivateAsync(email);

        var code = await RunCapturingAsync("reset-qa", cp);

        code.Should().Be(CommandRunner.Success, CauceLog);
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var current = await db.Users.AsNoTracking().SingleAsync(user => user.Email == email);
        current.Id.Should().NotBe(activated.Id);
        current.Status.Should().Be(UserStatus.PendingActivation);
        Factory.KeycloakClient.UpdatePasswordEmailsSent[^1].Should().Be(current.KeycloakId);
        Factory.KeycloakClient.DeletedUsers.Should().Contain(activated.KeycloakId);
        var retired = await db.Users.AsNoTracking().SingleAsync(user => user.Id == activated.Id);
        retired.Status.Should().Be(UserStatus.Inactive);
        retired.Email.Should().Be($"retired-{activated.Id}@retired.local");
        (await RunAsync("verify-qa", cp)).Should().Be(CommandRunner.Success);
    }

    [SkippableFact]
    public async Task ResetQa_ExclusiveNutritionistWithPatients_RecreatesBothAndRelinksThem()
    {
        SkipIfUnavailable();
        var (scope, db) = CreateDbScope();
        var before = await db.Users.AsNoTracking().SingleAsync(user => user.Email == "nutricionista.panel@ejemplo.com");
        scope.Dispose();

        (await RunAsync("reset-qa", "CP049")).Should().Be(CommandRunner.Success);

        var (checkScope, checkDb) = CreateDbScope();
        using var _ = checkScope;
        var after = await checkDb.Users.AsNoTracking().SingleAsync(user => user.Email == "nutricionista.panel@ejemplo.com");
        after.Id.Should().NotBe(before.Id);
        after.Status.Should().Be(UserStatus.Active);
        (await checkDb.NutritionistPatients.CountAsync(link => link.NutritionistId == before.Id && link.Status == AssignmentStatus.Active)).Should().Be(0);
        (await checkDb.NutritionistPatients.CountAsync(link => link.NutritionistId == after.Id && link.Status == AssignmentStatus.Active)).Should().BeGreaterThan(0);
        (await RunAsync("verify-qa", "CP049")).Should().Be(CommandRunner.Success);
    }

    private async Task<User> ActivateAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
        user.Activate();
        await db.SaveChangesAsync();
        return user;
    }
}
