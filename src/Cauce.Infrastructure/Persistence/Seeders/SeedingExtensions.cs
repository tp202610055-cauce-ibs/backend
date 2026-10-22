using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Métodos de extensión para ejecutar los seeders: los catálogos, que valen en cualquier ambiente, y las
/// cuentas de prueba, que son solo de desarrollo.
/// </summary>
public static class SeedingExtensions
{
    /// <summary>
    /// Carga los catálogos sin crear ninguna cuenta: roles, alergias, alimentos, glosario, documento de
    /// consentimiento, versiones de modelo y buckets. Es idempotente.
    /// </summary>
    /// <param name="services">Proveedor de servicios de un ámbito.</param>
    /// <param name="requireObjectStorage">Si es <see langword="true"/>, un MinIO caído propaga el error.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public static async Task SeedCatalogsAsync(
        this IServiceProvider services,
        bool requireObjectStorage,
        CancellationToken ct = default)
    {
        await services.GetRequiredService<UserRolesSeeder>().SeedAsync(ct).ConfigureAwait(false);
        await services.GetRequiredService<AllergiesSeeder>().SeedAsync(ct).ConfigureAwait(false);
        await services.GetRequiredService<FoodItemsSeeder>().SeedAsync(ct).ConfigureAwait(false);
        await services.GetRequiredService<GlossaryTermsSeeder>().SeedAsync(ct).ConfigureAwait(false);
        await services.GetRequiredService<ConsentDocumentsSeeder>().SeedAsync(ct).ConfigureAwait(false);
        await services.GetRequiredService<RecommendationsModelVersionsSeeder>().SeedAsync(ct).ConfigureAwait(false);

        var buckets = services.GetRequiredService<MinioBucketSeeder>();
        if (requireObjectStorage)
        {
            await buckets.EnsureBucketsAsync(ct).ConfigureAwait(false);
        }
        else
        {
            await buckets.SeedAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Aplica las migraciones pendientes, carga los catálogos y provisiona las cuentas de prueba. Pensado
    /// para invocarse solo en el entorno de desarrollo.
    /// </summary>
    /// <param name="serviceProvider">Proveedor de servicios raíz.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public static async Task RunDevelopmentSeedAsync(this IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentSeed");

        try
        {
            var context = services.GetRequiredService<CauceDbContext>();
            await context.Database.MigrateAsync(ct).ConfigureAwait(false);

            await services.SeedCatalogsAsync(requireObjectStorage: false, ct).ConfigureAwait(false);
            await services.GetRequiredService<DevAdminSeeder>().SeedAsync(ct).ConfigureAwait(false);
            await services.GetRequiredService<QaNutritionistsSeeder>().SeedAsync(ct).ConfigureAwait(false);
            await services.GetRequiredService<DemoPatientSeeder>().SeedAsync(ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Development seeding failed. The application will continue to start.");
        }

        // Ámbito propio: un fallo anterior puede dejar entidades pendientes en el contexto de la cadena.
        using var volumeScope = serviceProvider.CreateScope();
        try
        {
            await volumeScope.ServiceProvider.GetRequiredService<VolumePatientSeeder>().SeedAsync(ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Volume patient seeding failed. The application will continue to start.");
        }
    }
}
