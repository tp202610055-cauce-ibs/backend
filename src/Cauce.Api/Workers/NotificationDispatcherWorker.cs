using Cauce.Infrastructure.Notifications;

namespace Cauce.Api.Workers;

/// <summary>
/// Worker que, cada tick, delega el despacho del lote de notificaciones pendientes en
/// <see cref="NotificationBatchProcessor"/>, dentro de su propio ámbito. El scheduler (timer) es la
/// única responsabilidad que queda aquí; la lógica de despacho vive en el processor (acta A12).
/// </summary>
public sealed class NotificationDispatcherWorker : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationDispatcherWorker> _logger;

    /// <summary>
    /// Inicializa el worker con sus dependencias.
    /// </summary>
    /// <param name="scopeFactory">Fábrica de ámbitos de servicio.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="logger">Logger de la categoría del worker.</param>
    public NotificationDispatcherWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<NotificationDispatcherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Workers:NotificationDispatcher:Enabled", true))
        {
            _logger.LogInformation("NotificationDispatcherWorker is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while dispatching notifications.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                break;
            }
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<NotificationBatchProcessor>();
        await processor.DispatchDueAsync(DateTime.UtcNow, BatchSize, ct).ConfigureAwait(false);
    }
}
