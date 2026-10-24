using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Guardas de entorno de los comandos de QA: sin las variables o con contraseñas que no cumplen la política, salen
/// con el código 2 y no escriben nada. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class QaCommandGuardTests
    : QaCommandTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string WeakPassword = "corta";

    public QaCommandGuardTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    public static TheoryData<string?, string?, string?> RefusedEnvironments => new()
    {
        { null, "1", PatientPassword },
        { "0", "1", PatientPassword },
        { "1", null, PatientPassword },
        { "1", "1", null },
        { "1", "1", WeakPassword }
    };

    [SkippableTheory]
    [MemberData(nameof(RefusedEnvironments))]
    public async Task SeedQa_RefusedEnvironment_ExitsWithTwoAndWritesNothing(string? mode, string? destructive, string? patientPassword)
    {
        SkipIfUnavailable();
        var settings = new Dictionary<string, string?>(QaVariables)
        {
            ["QA_MODE"] = mode,
            ["QA_ALLOW_DESTRUCTIVE"] = destructive,
            ["QA_PATIENT_PASSWORD"] = patientPassword
        };
        await using var factory = new CustomWebApplicationFactory(Postgres.ConnectionString, Redis.ConnectionString, additionalSettings: settings);
        var manifest = WriteManifest(new QaManifestBuilder()
            .Patient("paciente.qa@ejemplo.com")
            .Case("CP901", ["paciente.qa@ejemplo.com"])
            .Build());

        var code = await RunCapturingAsync(factory, "seed-qa", "--manifest", manifest);

        code.Should().Be(CommandRunner.Refused);
        CapturedLog.Should().NotContain(line => line.Contains(PatientPassword) || line.Contains(WeakPassword, StringComparison.Ordinal));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Cauce.Infrastructure.Persistence.CauceDbContext>();
        (await db.Users.CountAsync()).Should().Be(0);
        factory.KeycloakClient.PasswordResets.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task SeedQa_UnknownOption_ExitsWithTwo()
    {
        SkipIfUnavailable();

        (await RunAsync("seed-qa", "--todo")).Should().Be(CommandRunner.Refused);
    }
}
