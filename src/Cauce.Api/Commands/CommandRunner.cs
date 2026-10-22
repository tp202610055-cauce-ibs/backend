using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.Commands;

/// <summary>
/// Comandos de operación que se ejecutan con el mismo binario de la API y terminan sin levantar el servidor
/// web: <c>dotnet Cauce.Api.dll migrate</c>. Ninguno crea cuentas.
/// </summary>
internal static class CommandRunner
{
    /// <summary>
    /// Código de salida de un comando que terminó bien.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Código de salida de un comando que falló.
    /// </summary>
    public const int Failure = 1;

    /// <summary>
    /// Código de salida de un comando desconocido.
    /// </summary>
    public const int UnknownCommand = 2;

    private static readonly IReadOnlyDictionary<string, Func<IServiceProvider, CancellationToken, Task>> Commands =
        new Dictionary<string, Func<IServiceProvider, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            ["migrate"] = MigrateAsync,
            ["seed-catalogs"] = SeedCatalogsAsync
        };

    /// <summary>
    /// Indica si los argumentos piden un comando y no el servidor. Las opciones (<c>--urls</c>) y las
    /// asignaciones de configuración (<c>clave=valor</c>) siguen siendo del servidor.
    /// </summary>
    /// <param name="args">Argumentos de la línea de comandos.</param>
    public static bool IsCommand(IReadOnlyList<string> args)
    {
        return args.Count > 0 && !args[0].StartsWith('-') && !args[0].Contains('=', StringComparison.Ordinal);
    }

    /// <summary>
    /// Ejecuta el comando pedido en un ámbito propio y devuelve el código de salida del proceso.
    /// </summary>
    /// <param name="services">Proveedor de servicios raíz de la aplicación.</param>
    /// <param name="args">Argumentos de la línea de comandos; el primero es el nombre del comando.</param>
    /// <param name="ct">Token de cancelación.</param>
    public static async Task<int> RunAsync(IServiceProvider services, IReadOnlyList<string> args, CancellationToken ct = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Commands");
        var name = args[0];

        if (!Commands.TryGetValue(name, out var command))
        {
            logger.LogError(
                "Unknown command {Command}. Available commands: {Commands}.",
                name,
                string.Join(", ", Commands.Keys));
            return UnknownCommand;
        }

        try
        {
            using var scope = services.CreateScope();
            await command(scope.ServiceProvider, ct).ConfigureAwait(false);
            logger.LogInformation("Command {Command} finished.", name);
            return Success;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Command {Command} failed.", name);
            return Failure;
        }
    }

    private static async Task MigrateAsync(IServiceProvider services, CancellationToken ct)
    {
        var context = services.GetRequiredService<CauceDbContext>();
        await context.Database.MigrateAsync(ct).ConfigureAwait(false);
    }

    private static Task SeedCatalogsAsync(IServiceProvider services, CancellationToken ct)
    {
        return services.SeedCatalogsAsync(requireObjectStorage: true, ct);
    }
}
