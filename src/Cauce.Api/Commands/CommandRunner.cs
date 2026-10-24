using Cauce.Infrastructure.ClinicalRegistry;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Seeders;
using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Qa.Manifest;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.Commands;

/// <summary>
/// Comandos de operación que se ejecutan con el mismo binario de la API y terminan sin levantar el servidor
/// web: <c>dotnet Cauce.Api.dll migrate</c>. Ninguno arranca los workers; solo los de QA crean cuentas.
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

    /// <summary>
    /// Código de salida de un comando que el entorno o sus argumentos no permiten ejecutar; no escribió nada.
    /// </summary>
    public const int Refused = 2;

    private static readonly IReadOnlyDictionary<string, Func<IServiceProvider, IReadOnlyList<string>, CancellationToken, Task>> Commands =
        BuildCommands();

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
            await command(scope.ServiceProvider, args.Skip(1).ToList(), ct).ConfigureAwait(false);
            logger.LogInformation("Command {Command} finished.", name);
            return Success;
        }
        catch (QaGuardException exception)
        {
            logger.LogError("Command {Command} refused: {Reason}", name, exception.Message);
            return Refused;
        }
        catch (QaFailureException exception)
        {
            logger.LogError("Command {Command} found failures: {Summary}", name, exception.Message);
            return Failure;
        }
        catch (QaManifestException exception)
        {
            logger.LogError(
                "Command {Command} stopped before writing; {Count} problem(s):{NewLine}{Problems}",
                name,
                exception.Errors.Count,
                Environment.NewLine,
                string.Join(Environment.NewLine, exception.Errors));
            return Failure;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Command {Command} failed.", name);
            return Failure;
        }
    }

    private static Dictionary<string, Func<IServiceProvider, IReadOnlyList<string>, CancellationToken, Task>> BuildCommands()
    {
        var commands = new Dictionary<string, Func<IServiceProvider, IReadOnlyList<string>, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            ["migrate"] = (services, _, ct) => MigrateAsync(services, ct),
            ["seed-catalogs"] = (services, _, ct) => SeedCatalogsAsync(services, ct),
            ["process-ibs-sss"] = (services, _, ct) => ProcessIbsSssAsync(services, ct)
        };

        var operations = typeof(CommandRunner).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IOperationCommand).IsAssignableFrom(type))
            .Select(type => (IOperationCommand)Activator.CreateInstance(type)!);
        foreach (var operation in operations)
        {
            commands.Add(operation.Name, operation.RunAsync);
        }

        return commands;
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

    // Una pasada del procesador diario con la hora actual; los push los envía el despachador de la API en marcha.
    private static async Task ProcessIbsSssAsync(IServiceProvider services, CancellationToken ct)
    {
        var processor = services.GetRequiredService<IbsSssScheduleProcessor>();
        var result = await processor.ProcessAsync(DateTime.UtcNow, ct).ConfigureAwait(false);

        services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Commands").LogInformation(
            "IBS-SSS schedules processed: {Reminders} 48-hour reminder(s), {DueNotices} due notice(s), {Missed} marked missed.",
            result.RemindersSent,
            result.DueNoticesSent,
            result.MarkedMissed);
    }
}
