using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Qa.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Qa;

/// <summary>
/// Pruebas de <c>inspect-qa</c> sobre el manifiesto publicado: el estado que piden los pasos del operador, sin escribir
/// nada y sin imprimir IP, códigos completos ni contraseñas. Incluye el archivado de CP075 visto por los tres comandos.
/// Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InspectQaCommandTests
    : QaCommandTestBase, IAsyncLifetime, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    public InspectQaCommandTests(PostgresFixture postgres, RedisFixture redis)
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
    public async Task InspectQa_Cp075_ShowsThePendingArchiveThenTheSystemArchivalAfterTheCommand()
    {
        SkipIfUnavailable();
        var before = await StateAsync();

        (await RunCapturingAsync("inspect-qa", "CP075", "--recs")).Should().Be(CommandRunner.Success, CauceLog);

        CommandLines().Should().Contain(line => line.Contains("creada por el nutricionista; activa; vigencia hasta") && line.Contains("vencida y pendiente de archivo"));
        (await StateAsync()).Should().BeEquivalentTo(before);

        CapturedLog.Clear();
        (await RunAsync("archive-recommendations")).Should().Be(CommandRunner.Success);
        (await RunCapturingAsync("inspect-qa", "CP075", "--recs")).Should().Be(CommandRunner.Success, CauceLog);
        CommandLines().Should().Contain(line => line.Contains("creada por el nutricionista; archivada el") && line.Contains("(por vigencia vencida)"));

        CapturedLog.Clear();
        (await RunCapturingAsync("audit-log", "CP075", "--action", "update")).Should().Be(CommandRunner.Success, CauceLog);
        CommandLines().Should().ContainSingle(line => line.Contains("| Modificación | Recomendación #"))
            .Which.Should().EndWith("| Sistema (proceso automático)");
    }

    [SkippableFact]
    public async Task InspectQa_Cp084_ShowsTheQuestionnaireDueTodayAndTheNoticeHour()
    {
        SkipIfUnavailable();

        (await RunCapturingAsync("inspect-qa", "CP084", "--ibs")).Should().Be(CommandRunner.Success, CauceLog);

        var lines = CommandLines();
        lines.Should().Contain(line => line.Contains("Cuestionarios respondidos: 2"));
        lines.Should().Contain(line => line.Contains("Cuestionario pendiente: le toca hoy") && line.Contains("aviso aún no enviado"));
        lines.Should().Contain(line => line.Contains("09:00 de Lima, todos los días; en QA lo dispara el operador con process-ibs-sss"));
    }

    [SkippableFact]
    public async Task InspectQa_Cp066AfterAccountDeletion_FindsTheAnonymizedPatientAndItsRetainedData()
    {
        SkipIfUnavailable();
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var patient = await db.Users.SingleAsync(user => user.Email == "paciente.eliminar@ejemplo.com");
            patient.Anonymize(patient.RoleId);
            await db.SaveChangesAsync();
        }

        (await RunCapturingAsync("inspect-qa", "CP066", "--state")).Should().Be(CommandRunner.Success, CauceLog);

        var lines = CommandLines();
        lines.Should().Contain(line => line.Contains("Cuenta: inactiva; correo anonimizado"));
        lines.Should().Contain(line => line.Contains("Datos que conserva: 1 comida(s), 1 síntoma(s)"));
    }

    [SkippableFact]
    public async Task InspectQa_WholeCase_PrintsNoIpNoFullCodeAndNoPassword()
    {
        SkipIfUnavailable();
        var (scope, db) = CreateDbScope();
        List<string> codes;
        List<string?> addresses;
        using (scope)
        {
            codes = await db.InvitationCodes.Select(code => code.Code).ToListAsync();
            addresses = await db.ConsentRecords.Where(consent => consent.IpAddress != null).Select(consent => consent.IpAddress).ToListAsync();
        }

        (await RunCapturingAsync("inspect-qa", "CP053")).Should().Be(CommandRunner.Success, CauceLog);
        (await RunCapturingAsync("inspect-qa", "CP001")).Should().Be(CommandRunner.Success, CauceLog);
        (await RunCapturingAsync("inspect-qa", "paciente.demo@ejemplo.com")).Should().Be(CommandRunner.Success, CauceLog);

        var lines = CommandLines();
        lines.Should().Contain(line => line.Contains("Códigos de invitación: 5"));
        lines.Should().Contain(line => line.Contains("registro.cp001@ejemplo.com (no debe existir): no existe en el sistema."));
        lines.Should().Contain(line => line.Contains("Consentimiento: versión 1.0, aceptado el") && line.Contains("vigente"));
        codes.Should().NotBeEmpty();
        addresses.Should().NotBeEmpty();
        lines.Should().NotContain(line => codes.Any(code => line.Contains(code)) || addresses.Any(address => line.Contains(address!))
            || line.Contains(PatientPassword) || line.Contains(NutritionistPassword) || line.Contains(SmokePassword));
    }

    [SkippableTheory]
    [InlineData("CP075", "--terms")]
    [InlineData("CP075", "CP084")]
    [InlineData("--recs")]
    public async Task InspectQa_InvalidArguments_ExitsWithTwo(params string[] arguments)
    {
        SkipIfUnavailable();

        (await RunAsync(["inspect-qa", .. arguments])).Should().Be(CommandRunner.Refused);
    }

    [SkippableFact]
    public async Task InspectQa_WithoutQaMode_ExitsWithTwo()
    {
        SkipIfUnavailable();
        var settings = new Dictionary<string, string?>(QaVariables) { ["QA_MODE"] = null };
        await using var factory = new CustomWebApplicationFactory(Postgres.ConnectionString, Redis.ConnectionString, additionalSettings: settings);

        (await RunCapturingAsync(factory, "inspect-qa", "CP075")).Should().Be(CommandRunner.Refused);
    }

    private List<string> CommandLines() =>
        CapturedLog.Where(line => line.StartsWith("Cauce.Commands: ", StringComparison.Ordinal)).Select(line => line["Cauce.Commands: ".Length..]).ToList();

    private async Task<object> StateAsync()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            return new
            {
                Audit = await db.AuditLogs.CountAsync(),
                Users = await db.Users.AsNoTracking().OrderBy(user => user.Id).Select(user => new { user.Id, user.Status, user.UpdatedAt }).ToListAsync(),
                Recommendations = await db.Recommendations.AsNoTracking().OrderBy(recommendation => recommendation.Id)
                    .Select(recommendation => new { recommendation.Id, recommendation.Status, recommendation.IsActive }).ToListAsync(),
                Notifications = await db.Notifications.CountAsync(),
                Schedules = await db.IbsSssSchedules.AsNoTracking().Select(schedule => new { schedule.Id, schedule.Missed, schedule.ReminderSentAt }).ToListAsync()
            };
        }
    }
}
