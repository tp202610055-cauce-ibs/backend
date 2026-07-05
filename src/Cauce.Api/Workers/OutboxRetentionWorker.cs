using Cauce.Application.Common.Interfaces.Outbox;

namespace Cauce.Api.Workers;

/// <summary>
/// Worker de retención que elimina los mensajes de outbox ya procesados con más de treinta días de
/// antigüedad, para mantener acotada la tabla. Se ejecuta cada veinticuatro horas.
/// </summary>
public sealed class OutboxRetentionWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxRetentionWorker> _logger;

    /// <summary>
    /// Inicializa el worker con sus dependencias.
    /// </summary>
    /// <param name="scopeFactory">Fábrica de ámbitos de servicio.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="logger">Logger de la categoría del worker.</param>
    public OutboxRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OutboxRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Workers:OutboxRetention:Enabled", true))
        {
            _logger.LogInformation("OutboxRetentionWorker is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await PurgeAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while purging processed outbox messages.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var cutoff = DateTime.UtcNow - RetentionWindow;
        var deleted = await repository.DeleteProcessedOlderThanAsync(cutoff, ct).ConfigureAwait(false);
        if (deleted > 0)
        {
            _logger.LogInformation("Purged {Count} processed outbox message(s) older than 30 days.", deleted);
        }
    }
}
