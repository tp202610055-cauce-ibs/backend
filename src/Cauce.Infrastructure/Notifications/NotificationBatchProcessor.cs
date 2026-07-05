using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Notifications;

namespace Cauce.Infrastructure.Notifications;

/// <summary>
/// Despacha un lote de notificaciones pendientes por su canal (push/email), en orden de
/// <c>scheduled_for</c> (y, ante empate, por identificador — orden determinista), aplicando el
/// resultado a la máquina de estados de cada notificación (envío exitoso, o intento fallido con
/// backoff exponencial). Es servicio scoped: el <see cref="NotificationDispatcherWorker"/> lo invoca
/// en un ámbito por tick; las pruebas de integración lo invocan directamente (acta A12).
/// </summary>
public sealed class NotificationBatchProcessor
{
    private readonly INotificationRepository _repository;
    private readonly IReadOnlyList<INotificationSender> _senders;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el procesador con sus dependencias.
    /// </summary>
    /// <param name="repository">Repositorio de notificaciones.</param>
    /// <param name="senders">Proveedores de envío por canal.</param>
    /// <param name="unitOfWork">Unidad de trabajo.</param>
    public NotificationBatchProcessor(
        INotificationRepository repository,
        IEnumerable<INotificationSender> senders,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _senders = senders.ToList();
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Despacha las notificaciones vencidas hasta el tamaño de lote indicado.
    /// </summary>
    /// <param name="now">Marca de tiempo UTC de referencia.</param>
    /// <param name="batchSize">Tamaño del lote.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La cantidad de notificaciones procesadas en el lote.</returns>
    public async Task<int> DispatchDueAsync(DateTime now, int batchSize, CancellationToken ct = default)
    {
        var pending = await _repository.ListDispatchableAsync(now, batchSize, ct).ConfigureAwait(false);
        if (pending.Count == 0)
        {
            return 0;
        }

        foreach (var notification in pending)
        {
            var sender = _senders.FirstOrDefault(candidate => candidate.Channel == notification.Channel);
            if (sender is null)
            {
                notification.RecordFailedAttempt($"Sin proveedor para el canal {notification.Channel}.", now);
                continue;
            }

            var result = await sender.SendAsync(notification, ct).ConfigureAwait(false);
            if (result.Success)
            {
                notification.MarkAsSent(now);
            }
            else
            {
                notification.RecordFailedAttempt(result.ErrorMessage ?? "Error de envío desconocido.", now);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return pending.Count;
    }
}
