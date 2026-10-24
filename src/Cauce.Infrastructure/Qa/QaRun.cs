using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Identity;
using Cauce.Infrastructure.Qa.Manifest;
using Cauce.Infrastructure.Qa.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Qa;

/// <summary>
/// Una ejecución de siembra de QA: manifiesto y catálogo comprobados, contraseñas validadas y un único ancla de reloj.
/// Toda comprobación ocurre en <see cref="PrepareAsync"/>, antes de escribir nada.
/// </summary>
public sealed class QaRun
{
    private readonly IServiceProvider _services;
    private readonly CauceDbContext _db;
    private readonly IKeycloakAdminClient _keycloak;
    private readonly QaAccountProvisioner _provisioner;
    private readonly IReadOnlyList<IQaSeedApplier> _appliers;
    private readonly ILogger _logger;

    private QaRun(
        IServiceProvider services,
        QaManifest manifest,
        QaCatalog catalog,
        QaClock clock,
        QaSettings settings,
        IReadOnlyList<IQaSeedApplier> appliers)
    {
        _services = services;
        Manifest = manifest;
        Catalog = catalog;
        Clock = clock;
        Settings = settings;
        _appliers = appliers;
        _db = services.GetRequiredService<CauceDbContext>();
        _keycloak = services.GetRequiredService<IKeycloakAdminClient>();
        _provisioner = ActivatorUtilities.CreateInstance<QaAccountProvisioner>(services);
        _logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Qa");
    }

    /// <summary>
    /// Proveedor de servicios del ámbito de la ejecución.
    /// </summary>
    public IServiceProvider Services => _services;

    /// <summary>
    /// Manifiesto en uso.
    /// </summary>
    public QaManifest Manifest { get; }

    /// <summary>
    /// Catálogo comprobado contra el manifiesto.
    /// </summary>
    public QaCatalog Catalog { get; }

    /// <summary>
    /// Reloj de la ejecución, anclado al instante en que se preparó.
    /// </summary>
    public QaClock Clock { get; }

    /// <summary>
    /// Variables de entorno de QA.
    /// </summary>
    public QaSettings Settings { get; }

    /// <summary>
    /// Retiro de cuentas de la ejecución.
    /// </summary>
    public QaAccountRetirement Retirement => ActivatorUtilities.CreateInstance<QaAccountRetirement>(_services);

    /// <summary>
    /// Cuentas que siembra la ejecución: las del manifiesto y las de humo.
    /// </summary>
    public IReadOnlyList<QaAccount> SeededAccounts => Manifest.SeededAccounts.Concat(QaSmokeAccounts.All).ToList();

    /// <summary>
    /// Comprueba todo lo que puede fallar antes de escribir: guardas, manifiesto, catálogo, contraseñas y aplicadores.
    /// </summary>
    /// <exception cref="QaGuardException">Si el entorno no lo permite o faltan contraseñas válidas.</exception>
    /// <exception cref="QaManifestException">Si el manifiesto o el catálogo tienen problemas.</exception>
    public static async Task<QaRun> PrepareAsync(IServiceProvider services, string? manifestPath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(services);
        var settings = new QaSettings(services.GetRequiredService<IConfiguration>());
        settings.EnsureDestructiveAllowed();

        var manifest = QaManifestLoader.LoadFile(QaManifestLocator.Resolve(manifestPath, AppContext.BaseDirectory));

        var catalog = await QaCatalog.LoadAsync(services.GetRequiredService<CauceDbContext>(), ct).ConfigureAwait(false);
        var missing = catalog.FindMissing(manifest);
        if (missing.Count > 0)
        {
            throw new QaManifestException(missing);
        }

        var appliers = QaSeedAppliers.Create(services);
        var unsupported = QaSeedAppliers.FindUnsupported(manifest, appliers);
        if (unsupported.Count > 0)
        {
            throw new QaManifestException(unsupported.Select(type => $"No hay cómo sembrar el tipo {type}.").ToList());
        }

        await ValidatePasswordsAsync(services, settings, manifest, ct).ConfigureAwait(false);

        var clock = new QaClock(services.GetRequiredService<TimeProvider>().GetUtcNow());
        return new QaRun(services, manifest, catalog, clock, settings, appliers);
    }

    /// <summary>
    /// Crea la cuenta con sus datos o, si ya existe, solo la deja en el estado del manifiesto. Guarda al terminar.
    /// Si una cuenta nueva falla a medias, borra su identidad en Keycloak y no guarda nada de ella.
    /// </summary>
    /// <param name="account">Cuenta a sembrar.</param>
    /// <param name="onlyCases">Casos cuyos datos se siembran; <see langword="null"/> para todos.</param>
    /// <param name="ct">Token de cancelación.</param>
    public async Task<QaProvisionedAccount> SeedAccountAsync(QaAccount account, IEnumerable<string>? onlyCases, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(account);
        var provisioned = await _provisioner.EnsureAsync(account, PasswordFor(account), Clock, ct).ConfigureAwait(false);
        if (!provisioned.Created)
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return provisioned;
        }

        try
        {
            await ApplySeedsAsync(provisioned.User, account.Email, onlyCases, ct).ConfigureAwait(false);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return provisioned;
        }
        catch
        {
            _db.ChangeTracker.Clear();
            await TryDeleteIdentityAsync(provisioned.User.KeycloakId).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Siembra los datos de los casos indicados sobre una cuenta existente, sin guardar.
    /// </summary>
    public async Task ApplySeedsAsync(Domain.Identity.User owner, string email, IEnumerable<string>? onlyCases, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var context = new QaSeedContext(_db, Clock, Catalog, Manifest, owner);
        var owned = QaSeedOwnership.OwnedBy(Manifest, email, onlyCases)
            .Select(pair => (pair.Cp, pair.Seed, Applier: _appliers.First(applier => applier.CanApply(pair.Seed))))
            .OrderBy(entry => entry.Applier.Order)
            .ToList();

        foreach (var (cp, seed, applier) in owned)
        {
            context.Cp = cp;
            await applier.ApplyAsync(seed, context, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Contraseña que corresponde a la cuenta según su rol y si es de humo.
    /// </summary>
    public string PasswordFor(QaAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return account.Status == QaAccountStatus.PendingActivation
            ? string.Empty
            : Settings.PasswordFor(account.Role ?? QaRole.Patient, QaSmokeAccounts.IsSmoke(account.Email));
    }

    private static async Task ValidatePasswordsAsync(
        IServiceProvider services, QaSettings settings, QaManifest manifest, CancellationToken ct)
    {
        var accounts = manifest.SeededAccounts.Concat(QaSmokeAccounts.All)
            .Where(account => account.Status != QaAccountStatus.PendingActivation)
            .ToList();
        var groups = accounts
            .GroupBy(account => QaSettings.PasswordKeyFor(account.Role ?? QaRole.Patient, QaSmokeAccounts.IsSmoke(account.Email)))
            .ToList();

        var problems = new List<string>();
        var passwords = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            try
            {
                var first = group.First();
                passwords[group.Key] = settings.PasswordFor(first.Role ?? QaRole.Patient, QaSmokeAccounts.IsSmoke(first.Email));
            }
            catch (QaGuardException exception)
            {
                problems.Add(exception.Message);
            }
        }

        if (problems.Count > 0)
        {
            throw new QaGuardException(string.Join(" ", problems));
        }

        var policy = await services.GetRequiredService<IQaKeycloakAdmin>().GetPasswordPolicyAsync(ct).ConfigureAwait(false);
        foreach (var group in groups)
        {
            var violations = QaPasswordPolicy.FindViolations(passwords[group.Key], policy, group.Select(account => account.Email));
            if (violations.Count > 0)
            {
                problems.Add($"{group.Key} no cumple la política del realm: {string.Join(", ", violations)}.");
            }
        }

        if (problems.Count > 0)
        {
            throw new QaGuardException(string.Join(" ", problems));
        }
    }

    private async Task TryDeleteIdentityAsync(string keycloakId)
    {
        try
        {
            await _keycloak.DeleteUserAsync(keycloakId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Could not delete the Keycloak identity of a QA account that failed halfway.");
        }
    }
}
