using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.Workers;

/// <summary>
/// Worker que archiva a diario (03:00 UTC) las recomendaciones cuya vigencia (<c>ValidUntil</c>) venció
/// y que siguen activas en un estado terminal aprobado (US30 CA02). El archivado se modela como
/// <c>IsActive = false</c> con <see cref="ArchiveReason.TemporalExpiration"/> (acta A22). La actualización
/// masiva dispara el trigger de auditoría; el actor queda nulo por tratarse del sistema.
/// </summary>
public sealed class RecommendationArchivalWorker : BackgroundService
{
    private const int RunHourUtc = 3;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly RecommendationStatus[] ApprovedTerminalStatuses =
    [
        RecommendationStatus.Approved,
        RecommendationStatus.ModifiedApproved,
        RecommendationStatus.ManualApproved
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecommendationArchivalWorker> _logger;

    /// <summary>
    /// Inicializa el worker con sus dependencias.
    /// </summary>
    /// <param name="scopeFactory">Fábrica de ámbitos de servicio.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="logger">Logger de la categoría del worker.</param>
    public RecommendationArchivalWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RecommendationArchivalWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Workers:RecommendationArchival:Enabled", true))
        {
            _logger.LogInformation("RecommendationArchivalWorker is disabled by configuration.");
            return;
        }

        try
        {
            await Task.Delay(ComputeInitialDelay(DateTime.UtcNow), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await ArchiveExpiredAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while archiving recommendations.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Calcula el retardo hasta la próxima ejecución programada (las 03:00 UTC del día actual o el
    /// siguiente).
    /// </summary>
    /// <param name="nowUtc">Momento actual, en UTC.</param>
    /// <returns>El retardo hasta la próxima ejecución.</returns>
    internal static TimeSpan ComputeInitialDelay(DateTime nowUtc)
    {
        var todayRun = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, RunHourUtc, 0, 0, DateTimeKind.Utc);
        var nextRun = nowUtc <= todayRun ? todayRun : todayRun.AddDays(1);
        return nextRun - nowUtc;
    }

    private async Task ArchiveExpiredAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        var affected = await ArchiveDueAsync(context, DateTime.UtcNow, ct).ConfigureAwait(false);
        if (affected > 0)
        {
            _logger.LogInformation("Archived {Count} expired recommendation(s).", affected);
        }
    }

    /// <summary>
    /// Archiva, mediante una actualización masiva, las recomendaciones activas en estado terminal
    /// aprobado cuya vigencia venció. Expuesto para pruebas deterministas del criterio de archivado.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="nowUtc">Momento de referencia, en UTC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La cantidad de recomendaciones archivadas.</returns>
    internal static Task<int> ArchiveDueAsync(CauceDbContext context, DateTime nowUtc, CancellationToken ct)
    {
        return context.Recommendations
            .Where(recommendation => recommendation.IsActive
                && recommendation.ValidUntil != null
                && recommendation.ValidUntil < nowUtc
                && ApprovedTerminalStatuses.Contains(recommendation.Status))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(recommendation => recommendation.IsActive, false)
                    .SetProperty(recommendation => recommendation.ArchiveReason, ArchiveReason.TemporalExpiration)
                    .SetProperty(recommendation => recommendation.ArchivedAt, nowUtc),
                ct);
    }
}
