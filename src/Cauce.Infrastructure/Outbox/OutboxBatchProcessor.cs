using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Outbox;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Outbox;

/// <summary>
/// Procesa un mensaje de outbox: lo resuelve a una notificación de MediatR, la publica y actualiza su
/// estado (procesado, o intento fallido con backoff/envenenamiento), persistiendo en una sola
/// transacción. Es servicio scoped: el <see cref="OutboxDispatcherWorker"/> lo invoca en un ámbito por
/// mensaje para aislar fallos; las pruebas de integración lo invocan directamente para determinismo
/// (acta A12). El instante se toma de <see cref="TimeProvider"/> para poder simularlo en pruebas.
/// </summary>
public sealed class OutboxBatchProcessor
{
    private readonly IOutboxRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OutboxEventTypeRegistry _registry;
    private readonly IPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxBatchProcessor> _logger;

    /// <summary>
    /// Inicializa el procesador con sus dependencias.
    /// </summary>
    /// <param name="repository">Repositorio de outbox.</param>
    /// <param name="unitOfWork">Unidad de trabajo.</param>
    /// <param name="registry">Registro de tipos de eventos.</param>
    /// <param name="publisher">Publicador de MediatR.</param>
    /// <param name="timeProvider">Proveedor de tiempo.</param>
    /// <param name="logger">Logger de la categoría del procesador.</param>
    public OutboxBatchProcessor(
        IOutboxRepository repository,
        IUnitOfWork unitOfWork,
        OutboxEventTypeRegistry registry,
        IPublisher publisher,
        TimeProvider timeProvider,
        ILogger<OutboxBatchProcessor> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _registry = registry;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Procesa un único mensaje de outbox y persiste el resultado.
    /// </summary>
    /// <param name="outboxId">Identificador del mensaje.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task ProcessAsync(Guid outboxId, CancellationToken ct = default)
    {
        var message = await _repository.GetByIdAsync(outboxId, ct).ConfigureAwait(false);
        if (message is null || message.ProcessedAt is not null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            var notification = _registry.ToNotification(message.EventType, message.PayloadJson);
            if (notification is null)
            {
                message.RecordFailedAttempt($"Tipo de evento no registrado: {message.EventType}", now);
            }
            else
            {
                await _publisher.Publish(notification, ct).ConfigureAwait(false);
                message.MarkAsProcessed(now);
            }
        }
        catch (Exception exception)
        {
            var poisoned = message.RecordFailedAttempt(exception.Message, now);
            if (poisoned)
            {
                _logger.LogError(
                    exception,
                    "Outbox message {OutboxId} poisoned after reaching the maximum attempts.",
                    outboxId);
            }
            else
            {
                _logger.LogWarning(exception, "Failed to dispatch outbox message {OutboxId}; will retry.", outboxId);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
