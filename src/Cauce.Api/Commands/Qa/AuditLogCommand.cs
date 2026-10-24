using System.Globalization;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Qa.Inspection;
using Cauce.Infrastructure.Qa.Manifest;

namespace Cauce.Api.Commands.Qa;

/// <summary>
/// <c>audit-log &lt;CPxxx | correo&gt; [--since &lt;6h | 2d | fecha de Lima&gt;] [--action &lt;tipo&gt;[,&lt;tipo&gt;]] [--limit N]</c>:
/// muestra, sin escribir, el registro de auditoría de las cuentas de un caso. Exige <c>QA_MODE=1</c>.
/// </summary>
internal sealed class AuditLogCommand : IOperationCommand
{
    private const string SinceOption = "--since";
    private const string ActionOption = "--action";
    private const string LimitOption = "--limit";
    private const string DefaultSince = "24h";
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;

    /// <inheritdoc />
    public string Name => "audit-log";

    /// <inheritdoc />
    public async Task RunAsync(IServiceProvider services, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        new QaSettings(services.GetRequiredService<IConfiguration>()).EnsureQaMode();
        var parsed = QaInspectionArguments.Parse(Name, arguments, [SinceOption, ActionOption, LimitOption], []);
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var since = QaInspectionArguments.ParseSince(parsed.Values.GetValueOrDefault(SinceOption, DefaultSince), now);
        var limit = parsed.Values.TryGetValue(LimitOption, out var rawLimit) ? QaInspectionArguments.ParseLimit(LimitOption, rawLimit, MaxLimit) : DefaultLimit;
        var actions = parsed.Values.TryGetValue(ActionOption, out var rawActions) ? ParseActions(rawActions) : [];

        var manifest = parsed.LoadManifest();
        var db = services.GetRequiredService<CauceDbContext>();
        var scope = await QaInspectionScope.ResolveAsync(manifest, parsed.Target, db, services.GetRequiredService<IKeycloakAdminClient>(), ct)
            .ConfigureAwait(false);
        var rows = await new QaAuditLogReader(db).ReadAsync(scope, since, actions, limit, ct).ConfigureAwait(false);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Commands");
        var sinceLima = (since + QaClock.LimaOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        logger.LogInformation(
            "audit-log {Target}: {Count} fila(s) desde {Since} (hora de Lima), las {Limit} más recientes como máximo.",
            scope.Target, rows.Count, sinceLima, limit);
        foreach (var row in rows)
        {
            logger.LogInformation("{Line}", QaAuditLogFormatter.FormatLine(row, scope));
        }

        foreach (var note in Notes(scope, rows))
        {
            logger.LogInformation("Nota: {Note}", note);
        }
    }

    private static IEnumerable<string> Notes(QaInspectionScope scope, IReadOnlyList<QaAuditRow> rows)
    {
        if (scope.Accounts.All(account => account.UserId is null))
        {
            yield return "ninguna cuenta del caso existe todavía en el sistema.";
        }

        if (rows.Any(row => row.Match == QaAuditMatch.UnknownEmail))
        {
            yield return "un ingreso con un correo sin cuenta no guarda el correo: salen todos los de la ventana, de cualquier revisor.";
        }

        if (rows.Any(row => row.Match == QaAuditMatch.MaskedEmail))
        {
            yield return "la solicitud de recuperación guarda solo la primera letra y el dominio del correo; no distingue cuentas que los compartan.";
        }

        foreach (var shared in scope.Accounts.Where(account => account.Shared && account.UserId is not null))
        {
            yield return $"{shared.Email} se usa en otros casos: de ella salen sus ingresos y lo que hizo con los datos del caso. Acota la ventana con --since.";
        }
    }

    private static IReadOnlyCollection<AuditActionType> ParseActions(string value)
    {
        var names = Enum.GetValues<AuditActionType>().ToDictionary(action => Normalize(action.ToString()), action => action);
        var actions = new List<AuditActionType>();
        foreach (var raw in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!names.TryGetValue(Normalize(raw), out var action))
            {
                throw new QaGuardException($"Tipo de acción desconocido: {raw}. Válidos: {string.Join(", ", names.Values)}.");
            }

            actions.Add(action);
        }

        return actions;
    }

    private static string Normalize(string name) => name.Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
