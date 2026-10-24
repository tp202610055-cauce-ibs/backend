using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Qa.Inspection;

namespace Cauce.Api.Commands.Qa;

/// <summary>
/// <c>inspect-qa &lt;CPxxx | correo&gt; [--consent] [--meals] [--ibs] [--codes] [--recs] [--state]</c>: muestra, sin
/// escribir, el estado de los datos de un caso. Sin banderas muestra todas las secciones. Exige <c>QA_MODE=1</c>.
/// </summary>
internal sealed class InspectQaCommand : IOperationCommand
{
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
        var parsed = QaInspectionArguments.Parse(Name, arguments, [], [.. Flags.Keys]);
        var sections = parsed.Flags.Aggregate(QaInspectSections.None, (all, flag) => all | Flags[flag]);
        if (sections == QaInspectSections.None)
        {
            sections = QaInspectSections.All;
        }

        var manifest = parsed.LoadManifest();
        var db = services.GetRequiredService<CauceDbContext>();
        var scope = await QaInspectionScope.ResolveAsync(manifest, parsed.Target, db, services.GetRequiredService<IKeycloakAdminClient>(), ct)
            .ConfigureAwait(false);
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var lines = await new QaStateInspector(db).InspectAsync(scope, sections, now, ct).ConfigureAwait(false);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Commands");
        var names = Flags.Where(flag => sections.HasFlag(flag.Value)).Select(flag => flag.Key);
        logger.LogInformation("inspect-qa {Target}: {Sections}.", scope.Target, string.Join(" ", names));
        foreach (var line in lines)
        {
            logger.LogInformation("{Line}", line);
        }
    }
}
