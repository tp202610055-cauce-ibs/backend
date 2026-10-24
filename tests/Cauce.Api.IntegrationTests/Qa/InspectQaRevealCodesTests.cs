using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Pruebas de <c>inspect-qa --reveal-codes</c>: solo los códigos que siembra el manifiesto salen completos, los demás
/// siguen enmascarados, la bandera implica <c>--codes</c> y no escribe nada. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InspectQaRevealCodesTests
    : QaCommandTestBase, IAsyncLifetime, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string SeededNutritionist = "nutricionista.codigo2@ejemplo.com";
    private const string PortalNutritionist = "nutricionista.codigo1@ejemplo.com";

    public InspectQaRevealCodesTests(PostgresFixture postgres, RedisFixture redis)
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
    public async Task RevealCodes_CaseWithSeededCodes_ShowsThemInFullWithTheirStateAndChangesNothing()
    {
        SkipIfUnavailable();
        var codes = await CodesOfAsync(SeededNutritionist);
        var audit = await AuditCountAsync();

        (await RunCapturingAsync("inspect-qa", "CP088", "--reveal-codes")).Should().Be(CommandRunner.Success, CauceLog);

        var lines = CommandLines();
        codes.Should().HaveCount(5);
        foreach (var code in codes)
        {
            lines.Should().Contain(line => line.StartsWith($"    {code}: ", StringComparison.Ordinal));
        }

        lines.Should().Contain(line => line.Contains(": vigente;"));
        lines.Should().Contain(line => line.Contains(": vencido;"));
        lines.Should().Contain(line => line.Contains(": canjeado el"));
        lines.Should().Contain("inspect-qa CP088: --codes.");
        lines.Should().Contain(line => line.StartsWith("Nota: los códigos completos solo se entregan al revisor", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("Comidas") || line.Contains("Consentimiento"));
        (await AuditCountAsync()).Should().Be(audit);
    }

    [SkippableFact]
    public async Task RevealCodes_NutritionistWithoutSeededCodes_KeepsTheCodesMasked()
    {
        SkipIfUnavailable();
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var nutritionist = await db.Users.SingleAsync(user => user.Email == PortalNutritionist);
            db.InvitationCodes.Add(InvitationCode.Generate(Guid.NewGuid(), "PORTAL42", nutritionist.Id, DateTime.UtcNow, TimeSpan.FromHours(72)));
            await db.SaveChangesAsync();
        }

        (await RunCapturingAsync("inspect-qa", PortalNutritionist, "--reveal-codes")).Should().Be(CommandRunner.Success, CauceLog);

        var lines = CommandLines();
        lines.Should().Contain(line => line.StartsWith("    PO****42: vigente;", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("PORTAL42") || line.StartsWith("Nota: los códigos completos", StringComparison.Ordinal));
    }

    [SkippableFact]
    public async Task InspectQa_WithoutRevealFlag_KeepsSeededCodesMasked()
    {
        SkipIfUnavailable();
        var codes = await CodesOfAsync(SeededNutritionist);

        (await RunCapturingAsync("inspect-qa", "CP088", "--codes")).Should().Be(CommandRunner.Success, CauceLog);

        CommandLines().Should().NotContain(line => codes.Any(code => line.Contains(code)));
    }

    private List<string> CommandLines() =>
        CapturedLog.Where(line => line.StartsWith("Cauce.Commands: ", StringComparison.Ordinal)).Select(line => line["Cauce.Commands: ".Length..]).ToList();

    private async Task<List<string>> CodesOfAsync(string email)
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var nutritionist = await db.Users.SingleAsync(user => user.Email == email);
            return await db.InvitationCodes.Where(code => code.NutritionistId == nutritionist.Id).Select(code => code.Code).ToListAsync();
        }
    }

    private async Task<int> AuditCountAsync()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            return await db.AuditLogs.CountAsync();
        }
    }
}
