using Cauce.Application.Common.Interfaces.Outbox;
using Cauce.Infrastructure.Outbox;

namespace Cauce.Api.Workers;

/// <summary>
/// Worker que publica los mensajes del outbox: lee los pendientes elegibles y delega el despacho de
/// cada uno en <see cref="OutboxBatchProcessor"/>, en su propio <see cref="IServiceScope"/> (aísla
/// fallos y no reprocesa los ya marcados en el mismo tick). El efecto exactly-once lo garantizan los
/// handlers verificando existencia previa (DEC-B5-05). El scheduler (timer) es la única
/// responsabilidad que queda aquí; la lógica de procesamiento vive en el processor (acta A12).
/// </summary>
public sealed class OutboxDispatcherWorker : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxDispatcherWorker> _logger;

    /// <summary>
    /// Inicializa el worker con sus dependencias.
    /// </summary>
    /// <param name="scopeFactory">Fábrica de ámbitos de servicio.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="timeProvider">Proveedor de tiempo.</param>
    /// <param name="logger">Logger de la categoría del worker.</param>
    public OutboxDispatcherWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<OutboxDispatcherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Workers:OutboxDispatcher:Enabled", true))
        {
            _logger.LogInformation("OutboxDispatcherWorker is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while dispatching outbox messages.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                break;
            }
        }
    }

    private async Task DispatchPendingAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        IReadOnlyList<Guid> pendingIds;
        using (var scope = _scopeFactory.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            pendingIds = await repository.ListPendingIdsAsync(now, BatchSize, ct).ConfigureAwait(false);
        }

        foreach (var id in pendingIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>();
            await processor.ProcessAsync(id, ct).ConfigureAwait(false);
        }
    }
}
