using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Qa.Inspection;
using Cauce.Infrastructure.Qa.Manifest;
using Cauce.Infrastructure.Qa.Seeding;

namespace Cauce.Api.Commands.Qa;

/// <summary>
/// <c>inspect-qa &lt;CPxxx | correo&gt; [--consent] [--meals] [--ibs] [--codes] [--recs] [--state] [--reveal-codes]</c>: muestra,
/// sin escribir, el estado de los datos de un caso. Sin banderas muestra todas las secciones. Exige <c>QA_MODE=1</c>.
/// </summary>
internal sealed class InspectQaCommand : IOperationCommand
{
    private const string RevealCodesFlag = "--reveal-codes";

    private static readonly Dictionary<string, QaInspectSections> Flags = new(StringComparer.Ordinal)
    {
        ["--consent"] = QaInspectSections.Consent,
        ["--meals"] = QaInspectSections.Meals,
        ["--ibs"] = QaInspectSections.Ibs,
        ["--codes"] = QaInspectSections.Codes,
        ["--recs"] = QaInspectSections.Recs,
        ["--state"] = QaInspectSections.State
    };

    /// <inheritdoc />
    public string Name => "inspect-qa";

    /// <inheritdoc />
    public async Task RunAsync(IServiceProvider services, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        new QaSettings(services.GetRequiredService<IConfiguration>()).EnsureQaMode();
        var parsed = QaInspectionArguments.Parse(Name, arguments, [], [.. Flags.Keys, RevealCodesFlag]);
        var reveal = parsed.Flags.Contains(RevealCodesFlag);
        var sections = parsed.Flags.Where(Flags.ContainsKey).Aggregate(reveal ? QaInspectSections.Codes : QaInspectSections.None, (all, flag) => all | Flags[flag]);
        if (sections == QaInspectSections.None)
        {
            sections = QaInspectSections.All;
        }

        var manifest = parsed.LoadManifest();
        var db = services.GetRequiredService<CauceDbContext>();
        var scope = await QaInspectionScope.ResolveAsync(manifest, parsed.Target, db, services.GetRequiredService<IKeycloakAdminClient>(), ct)
            .ConfigureAwait(false);
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        // Solo los códigos que siembra el manifiesto: el operador se los entrega al revisor, que no puede verlos en el portal.
        var revealCodesOf = reveal
            ? scope.Accounts.Where(account => QaSeedOwnership.OwnedBy(manifest, account.Email).Any(pair => pair.Seed is QaInviteCodeSeed))
                .Select(account => account.Email).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
        var lines = await new QaStateInspector(db).InspectAsync(scope, sections, now, ct, revealCodesOf).ConfigureAwait(false);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Commands");
        var names = Flags.Where(flag => sections.HasFlag(flag.Value)).Select(flag => flag.Key);
        logger.LogInformation("inspect-qa {Target}: {Sections}.", scope.Target, string.Join(" ", names));
        foreach (var line in lines)
        {
            logger.LogInformation("{Line}", line);
        }

        if (revealCodesOf is { Count: > 0 })
        {
            logger.LogInformation("Nota: los códigos completos solo se entregan al revisor del caso, por un medio privado.");
        }
    }
}
