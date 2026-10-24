using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Manifest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Infrastructure.Qa.Seeding;

/// <summary>
/// Siembra un tipo de elemento del manifiesto sobre la cuenta dueña. Escribe en el contexto sin guardar.
/// </summary>
public interface IQaSeedApplier
{
    /// <summary>
    /// Orden de aplicación entre tipos: el perfil antes que los vínculos y los vínculos antes que los datos.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Indica si este aplicador siembra el elemento.
    /// </summary>
    bool CanApply(QaSeed seed);

    /// <summary>
    /// Siembra el elemento en el contexto, sin guardar.
    /// </summary>
    Task ApplyAsync(QaSeed seed, QaSeedContext context, CancellationToken ct);
}

/// <summary>
/// Estado compartido de la siembra de una cuenta: base de datos, reloj, catálogo y la cuenta dueña.
/// </summary>
public sealed class QaSeedContext
{
    /// <summary>
    /// Crea el contexto de siembra de una cuenta.
    /// </summary>
    public QaSeedContext(CauceDbContext db, QaClock clock, QaCatalog catalog, QaManifest manifest, User owner)
    {
        Db = db;
        Clock = clock;
        Catalog = catalog;
        Manifest = manifest;
        Owner = owner;
    }

    /// <summary>
    /// Contexto de base de datos de la ejecución.
    /// </summary>
    public CauceDbContext Db { get; }

    /// <summary>
    /// Reloj anclado de la ejecución.
    /// </summary>
    public QaClock Clock { get; }

    /// <summary>
    /// Catálogo de alimentos y alergias ya comprobado.
    /// </summary>
    public QaCatalog Catalog { get; }

    /// <summary>
    /// Manifiesto en uso.
    /// </summary>
    public QaManifest Manifest { get; }

    /// <summary>
    /// Cuenta dueña de los elementos que se siembran.
    /// </summary>
    public User Owner { get; }

    /// <summary>
    /// Caso al que pertenece el elemento que se siembra.
    /// </summary>
    public string Cp { get; set; } = string.Empty;

    /// <summary>
    /// Busca una cuenta local por correo; primero entre las que el contexto aún no guardó.
    /// </summary>
    public async Task<User?> FindUserAsync(string email, CancellationToken ct)
    {
        var normalized = EmailNormalization.Normalize(email);
        return Db.Users.Local.FirstOrDefault(user => user.Email == normalized)
            ?? await Db.Users.FirstOrDefaultAsync(user => user.Email == normalized, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Busca una cuenta local que la siembra necesita y falla con el caso si no existe.
    /// </summary>
    public async Task<User> RequireUserAsync(string email, CancellationToken ct) =>
        await FindUserAsync(email, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"{Cp}: falta la cuenta {email}; se siembra antes que sus datos.");
}

/// <summary>
/// Reúne los aplicadores de siembra de este ensamblado, en su orden.
/// </summary>
public static class QaSeedAppliers
{
    /// <summary>
    /// Crea una instancia de cada aplicador con las dependencias del proveedor de servicios.
    /// </summary>
    public static IReadOnlyList<IQaSeedApplier> Create(IServiceProvider services) =>
        typeof(IQaSeedApplier).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IQaSeedApplier).IsAssignableFrom(type))
            .Select(type => (IQaSeedApplier)ActivatorUtilities.CreateInstance(services, type))
            .OrderBy(applier => applier.Order)
            .ThenBy(applier => applier.GetType().Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Tipos de elemento del manifiesto que ningún aplicador sabe sembrar.
    /// </summary>
    public static IReadOnlyList<string> FindUnsupported(QaManifest manifest, IReadOnlyList<IQaSeedApplier> appliers) =>
        manifest.Cases
            .SelectMany(testCase => testCase.Seeds)
            .Where(seed => !appliers.Any(applier => applier.CanApply(seed)))
            .Select(seed => seed.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
