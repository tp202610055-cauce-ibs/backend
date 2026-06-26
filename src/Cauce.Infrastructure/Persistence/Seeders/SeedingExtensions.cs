using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Métodos de extensión para ejecutar los seeders de identidad al arranque.
/// </summary>
public static class SeedingExtensions
{
    /// <summary>
    /// Aplica las migraciones pendientes y ejecuta los seeders de desarrollo
    /// (catálogo de roles y nutricionista de prueba). Pensado para invocarse solo
    /// en el entorno de desarrollo.
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

            await services.GetRequiredService<UserRolesSeeder>().SeedAsync(ct).ConfigureAwait(false);
            await services.GetRequiredService<DevAdminSeeder>().SeedAsync(ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Development seeding failed. The application will continue to start.");
        }
    }
}
