using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Identity;
using Cauce.Infrastructure.Qa.Manifest;
using Cauce.Infrastructure.Qa.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Qa;

/// <summary>
/// Conteos del rearme de un objetivo. No incluye correos ni contraseñas.
/// </summary>
public sealed record QaResetSummary(int Recreated, int Realigned, int Retired, int NotRearmable);

/// <summary>
/// Rearme de casos o cuentas (<c>reset-qa</c>). Una cuenta exclusiva se retira y se vuelve a sembrar con sus datos;
/// una compartida no se borra: recupera su estado y su contraseña, y el caso vuelve a sembrar lo suyo sobre ella.
/// </summary>
public static class QaResetter
{
    /// <summary>
    /// Comprueba que todos los objetivos existan en el manifiesto antes de tocar nada.
    /// </summary>
    /// <exception cref="QaManifestException">Si algún objetivo no es un caso ni una cuenta del manifiesto.</exception>
    public static void EnsureKnownTargets(QaManifest manifest, IReadOnlyList<string> targets)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(targets);
        var unknown = targets
            .Where(target => manifest.FindCase(target) is null && manifest.FindAccount(target) is null && !QaSmokeAccounts.IsSmoke(target))
            .Select(target => $"Objetivo desconocido: {target}.")
            .ToList();
        if (targets.Count == 0)
        {
            unknown.Add("Indica al menos un caso (CPxxx) o un correo.");
        }

        if (unknown.Count > 0)
        {
            throw new QaManifestException(unknown);
        }
    }

    /// <summary>
    /// Rearma cada objetivo con el ancla de la ejecución.
    /// </summary>
    public static async Task<QaResetSummary> ResetAsync(QaRun run, IReadOnlyList<string> targets, ILogger logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(logger);
        EnsureKnownTargets(run.Manifest, targets);

        var total = new QaResetSummary(0, 0, 0, 0);
        foreach (var target in targets)
        {
            var testCase = run.Manifest.FindCase(target);
            var summary = testCase is not null
                ? await ResetCaseAsync(run, testCase, ct).ConfigureAwait(false)
                : await ResetAccountAsync(run, target, ct).ConfigureAwait(false);

            if (testCase is not null)
            {
                logger.LogInformation(
                    "Reset {Cp}: {Recreated} recreated, {Realigned} realigned, {Retired} retired, {NotRearmable} not rearmable.",
                    testCase.Cp, summary.Recreated, summary.Realigned, summary.Retired, summary.NotRearmable);
            }
            else
            {
                logger.LogInformation(
                    "Reset account target: {Recreated} recreated, {Realigned} realigned, {Retired} retired, {NotRearmable} not rearmable.",
                    summary.Recreated, summary.Realigned, summary.Retired, summary.NotRearmable);
            }

            total = new QaResetSummary(
                total.Recreated + summary.Recreated,
                total.Realigned + summary.Realigned,
                total.Retired + summary.Retired,
                total.NotRearmable + summary.NotRearmable);
        }

        return total;
    }

    private static async Task<QaResetSummary> ResetCaseAsync(QaRun run, QaCase testCase, CancellationToken ct)
    {
        var retirement = run.Retirement;
        var retired = 0;
        foreach (var email in testCase.MustNotExist)
        {
            retired += await retirement.RetireAsync(email, run.Clock.Anchor, ct).ConfigureAwait(false) == QaRetireOutcome.Retired ? 1 : 0;
        }

        var accounts = testCase.ReferencedEmails()
            .Select(run.Manifest.FindAccount)
            .OfType<QaAccount>()
            .Where(account => account.Disposition == QaDisposition.Seed)
            .OrderBy(account => QaSeedOwnership.ProcessingRank(run.Manifest, account))
            .ToList();

        var exclusive = accounts.Where(account => !QaSeedOwnership.IsShared(run.Manifest, account.Email)).ToList();
        var shared = accounts.Except(exclusive).ToList();
        var (recreated, notRearmable) = await RecreateAsync(run, exclusive, ct).ConfigureAwait(false);

        foreach (var account in shared)
        {
            await RealignSharedAsync(run, account, [testCase.Cp], ct).ConfigureAwait(false);
        }

        return new QaResetSummary(recreated, shared.Count + notRearmable, retired, notRearmable);
    }

    private static async Task<QaResetSummary> ResetAccountAsync(QaRun run, string email, CancellationToken ct)
    {
        var account = run.Manifest.FindAccount(email) ?? QaSmokeAccounts.All.First(smoke => string.Equals(smoke.Email, email, StringComparison.OrdinalIgnoreCase));
        if (account.Disposition == QaDisposition.MustNotExist)
        {
            var outcome = await run.Retirement.RetireAsync(email, run.Clock.Anchor, ct).ConfigureAwait(false);
            return new QaResetSummary(0, 0, outcome == QaRetireOutcome.Retired ? 1 : 0, 0);
        }

        if (QaSeedOwnership.IsShared(run.Manifest, account.Email))
        {
            await RealignSharedAsync(run, account, onlyCases: [], ct).ConfigureAwait(false);
            return new QaResetSummary(0, 1, 0, 0);
        }

        var (recreated, notRearmable) = await RecreateAsync(run, [account], ct).ConfigureAwait(false);
        return new QaResetSummary(recreated, notRearmable, 0, notRearmable);
    }

    // Primero se retiran todas, para que ningún vínculo nuevo apunte a una cuenta que está por retirarse.
    private static async Task<(int Recreated, int NotRearmable)> RecreateAsync(QaRun run, IReadOnlyList<QaAccount> accounts, CancellationToken ct)
    {
        var retirement = run.Retirement;
        var rearmable = new List<QaAccount>();
        var notRearmable = 0;
        foreach (var account in accounts)
        {
            var roleName = account.Role == QaRole.Patient ? UserRoles.Patient : UserRoles.Nutritionist;
            if (!retirement.Supports(roleName))
            {
                await RealignSharedAsync(run, account, onlyCases: [], ct).ConfigureAwait(false);
                notRearmable++;
                continue;
            }

            await retirement.RetireAsync(account.Email, run.Clock.Anchor, ct).ConfigureAwait(false);
            rearmable.Add(account);
        }

        foreach (var account in rearmable)
        {
            await run.SeedAccountAsync(account, onlyCases: null, ct).ConfigureAwait(false);
        }

        return (rearmable.Count, notRearmable);
    }

    // Una cuenta que no se borra: estado, contraseña y desbloqueo; las pendientes reciben un enlace nuevo.
    private static async Task RealignSharedAsync(QaRun run, QaAccount account, IReadOnlyList<string> onlyCases, CancellationToken ct)
    {
        var db = run.Services.GetRequiredService<CauceDbContext>();
        var provisioner = ActivatorUtilities.CreateInstance<QaAccountProvisioner>(run.Services);
        var email = EmailNormalization.Normalize(account.Email);
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email, ct).ConfigureAwait(false);
        if (user is null)
        {
            await run.SeedAccountAsync(account, onlyCases: null, ct).ConfigureAwait(false);
            return;
        }

        await provisioner.RealignAsync(user, account.Status ?? QaAccountStatus.Active, run.PasswordFor(account), ct, resendActivation: true)
            .ConfigureAwait(false);
        if (onlyCases.Count > 0)
        {
            await run.ApplySeedsAsync(user, account.Email, onlyCases, ct).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
