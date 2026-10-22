using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Minio.DataModel.Args;

namespace Cauce.Api.IntegrationTests.Commands;

/// <summary>
/// Pruebas de los comandos <c>migrate</c> y <c>seed-catalogs</c> con la configuración de Production: son
/// idempotentes y no crean cuentas aunque los seeders de cuentas estén encendidos. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CatalogCommandsTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public CatalogCommandsTests(PostgresFixture postgres, RedisFixture redis, MinioFixture minio)
        : base(postgres, redis)
    {
        _minio = minio;
    }

    /// <inheritdoc />
    protected override bool ExtraAvailable => _minio.IsAvailable;

    /// <inheritdoc />
    protected override CustomWebApplicationFactory CreateFactory() => new(
        Postgres.ConnectionString,
        Redis.ConnectionString,
        minioEndpoint: _minio.Endpoint,
        minioAccessKey: _minio.AccessKey,
        minioSecretKey: _minio.SecretKey);

    [SkippableFact]
    public async Task MigrateAndSeedCatalogs_RunTwiceInProduction_AreIdempotentAndCreateNoAccounts()
    {
        SkipIfUnavailable();
        using var production = ProductionFactory();
        var usersBefore = await CountUsersAsync(production);

        var firstMigrate = await CommandRunner.RunAsync(production.Services, ["migrate"]);
        var firstSeed = await CommandRunner.RunAsync(production.Services, ["seed-catalogs"]);
        var afterFirstRun = await CountCatalogsAsync(production);
        var secondMigrate = await CommandRunner.RunAsync(production.Services, ["migrate"]);
        var secondSeed = await CommandRunner.RunAsync(production.Services, ["seed-catalogs"]);
        var afterSecondRun = await CountCatalogsAsync(production);

        new[] { firstMigrate, firstSeed, secondMigrate, secondSeed }.Should().OnlyContain(code => code == CommandRunner.Success);
        afterFirstRun.Should().OnlyContain(count => count > 0);
        afterSecondRun.Should().Equal(afterFirstRun);
        (await CountUsersAsync(production)).Should().Be(usersBefore);
        Factory.KeycloakClient.PasswordResets.Should().BeEmpty();

        var minio = production.Services.GetRequiredService<IMinioClient>();
        foreach (var bucket in new[] { "clinical-reports", "patient-exports" })
        {
            (await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket))).Should().BeTrue(bucket);
        }
    }

    [SkippableFact]
    public async Task SeedCatalogs_WithObjectStorageUnreachable_ExitsWithFailure()
    {
        SkipIfUnavailable();
        using var production = ProductionFactory(minioEndpoint: "127.0.0.1:1");

        var code = await CommandRunner.RunAsync(production.Services, ["seed-catalogs"]);

        code.Should().Be(CommandRunner.Failure);
    }

    [SkippableFact]
    public async Task Run_UnknownCommand_ExitsWithUsageCode()
    {
        SkipIfUnavailable();

        var code = await CommandRunner.RunAsync(Factory.Services, ["seed-everything"]);

        code.Should().Be(CommandRunner.UnknownCommand);
    }

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "migrate" }, true)]
    [InlineData(new[] { "--urls", "http://+:8080" }, false)]
    [InlineData(new[] { "urls=http://+:8080" }, false)]
    public void IsCommand_DistinguishesCommandsFromServerArguments(string[] args, bool expected)
    {
        CommandRunner.IsCommand(args).Should().Be(expected);
    }

    // Production con todas las claves obligatorias y los seeders de cuentas encendidos, para probar que los
    // comandos no los ejecutan.
    private WebApplicationFactory<Program> ProductionFactory(string? minioEndpoint = null) =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevAdmin:Enabled"] = "true",
                ["DevAdmin:Email"] = "devadmin@cauce.local",
                ["QaNutritionists:Enabled"] = "true",
                ["DemoPatient:Enabled"] = "true",
                ["DemoPatient:Email"] = "demo@cauce.local",
                ["Keycloak:WebPortalClientSecret"] = "test-portal-secret",
                ["Storage:Minio:Endpoint"] = minioEndpoint ?? _minio.Endpoint,
                ["Storage:Minio:PublicEndpoint"] = minioEndpoint ?? _minio.Endpoint
            }));
        });

    private static async Task<int> CountUsersAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CauceDbContext>().Users.CountAsync();
    }

    private static async Task<int[]> CountCatalogsAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        return
        [
            await db.UserRoles.CountAsync(),
            await db.Allergies.CountAsync(),
            await db.FoodItems.CountAsync(),
            await db.GlossaryTerms.CountAsync(),
            await db.ConsentDocuments.CountAsync(),
            await db.ModelVersions.CountAsync()
        ];
    }
}
