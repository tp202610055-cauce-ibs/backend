using Cauce.Infrastructure.Qa;
using Cauce.Infrastructure.Recommendations;

namespace Cauce.Api.Commands.Qa;

/// <summary>
/// <c>archive-recommendations</c>: una pasada del archivado diario por vigencia vencida, con la misma lógica del worker.
/// Afecta a todas las cuentas. Exige <c>QA_MODE=1</c>.
/// </summary>
internal sealed class ArchiveRecommendationsCommand : IOperationCommand
{
    /// <inheritdoc />
    public string Name => "archive-recommendations";

    /// <inheritdoc />
    public async Task RunAsync(IServiceProvider services, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var now = RecommendationSweepGuard.Prepare(services, arguments, Name);
        var affected = await services.GetRequiredService<RecommendationSweeper>().ArchiveDueAsync(now, ct).ConfigureAwait(false);
        services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Commands").LogInformation(
            "archive-recommendations: {Count} recomendación(es) archivada(s) por vigencia vencida.", affected);
    }
}

/// <summary>
/// <c>expire-recommendations</c>: una pasada de la expiración de recomendaciones vencidas sin revisar o sin entregar,
/// con la misma lógica del worker. Afecta a todas las cuentas. Exige <c>QA_MODE=1</c>.
/// </summary>
internal sealed class ExpireRecommendationsCommand : IOperationCommand
{
    /// <inheritdoc />
    public string Name => "expire-recommendations";

    /// <inheritdoc />
    public async Task RunAsync(IServiceProvider services, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var now = RecommendationSweepGuard.Prepare(services, arguments, Name);
        var affected = await services.GetRequiredService<RecommendationSweeper>().ExpireDueAsync(now, ct).ConfigureAwait(false);
        services.GetRequiredService<ILoggerFactory>().CreateLogger("Cauce.Commands").LogInformation(
            "expire-recommendations: {Count} recomendación(es) pasada(s) a vencida.", affected);
    }
}

internal static class RecommendationSweepGuard
{
    // Comprueba el modo QA y que no haya argumentos; devuelve la hora de referencia de la pasada.
    public static DateTime Prepare(IServiceProvider services, IReadOnlyList<string> arguments, string name)
    {
        new QaSettings(services.GetRequiredService<IConfiguration>()).EnsureQaMode();
        if (arguments.Count > 0)
        {
            throw new QaGuardException($"{name} no recibe argumentos.");
        }

        return services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
    }
}
