using Cauce.Infrastructure.Qa.Identity;
using Cauce.Infrastructure.Qa.Manifest;
using Cauce.Infrastructure.Qa.Seeding;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Qa;

/// <summary>
/// Conteos de una siembra completa. No incluye correos ni contraseñas.
/// </summary>
public sealed record QaSeedSummary(int Created, int Realigned, int Retired, int RetireUnsupported);

/// <summary>
/// Siembra completa e idempotente (<c>seed-qa</c>): borra las cuentas que no deben existir, crea las que faltan con sus
/// datos y deja las existentes en el estado del manifiesto. Una segunda corrida no agrega datos.
/// </summary>
public static class QaSeeder
{
    /// <summary>
    /// Ejecuta la siembra completa con una ejecución ya preparada.
    /// </summary>
    public static async Task<QaSeedSummary> SeedAsync(QaRun run, ILogger logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(logger);

        var retirement = run.Retirement;
        int retired = 0, unsupported = 0;
        foreach (var account in run.Manifest.MustNotExistAccounts)
        {
            var outcome = await retirement.RetireAsync(account.Email, run.Clock.Anchor, ct).ConfigureAwait(false);
            retired += outcome == QaRetireOutcome.Retired ? 1 : 0;
            unsupported += outcome == QaRetireOutcome.UnsupportedRole ? 1 : 0;
        }

        int created = 0, realigned = 0;

        // Los nutricionistas primero: los vínculos y las revisiones de los pacientes los referencian.
        foreach (var account in run.SeededAccounts.OrderBy(account => QaSeedOwnership.ProcessingRank(run.Manifest, account)))
        {
            var result = await run.SeedAccountAsync(account, onlyCases: null, ct).ConfigureAwait(false);
            created += result.Created ? 1 : 0;
            realigned += result.Created ? 0 : 1;
        }

        var summary = new QaSeedSummary(created, realigned, retired, unsupported);
        logger.LogInformation(
            "QA seed finished: {Created} account(s) created, {Realigned} realigned, {Retired} retired, {Unsupported} not retirable.",
            summary.Created,
            summary.Realigned,
            summary.Retired,
            summary.RetireUnsupported);
        return summary;
    }
}
