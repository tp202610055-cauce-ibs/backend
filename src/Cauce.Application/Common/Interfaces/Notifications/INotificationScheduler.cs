using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;

namespace Cauce.Application.Common.Interfaces.Notifications;

/// <summary>
/// Agenda notificaciones para su entrega asíncrona. La escritura se enrolla en el
/// <c>ChangeTracker</c> del <c>UnitOfWork</c> del llamador (no llama <c>SaveChangesAsync</c>).
/// </summary>
public interface INotificationScheduler
{
    /// <summary>
    /// Agenda una notificación.
    /// </summary>
    /// <param name="notification">Notificación a agendar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task ScheduleAsync(Notification notification, CancellationToken ct = default);

    /// <summary>
    /// Indica si ya existe una notificación para la entidad y el tipo indicados, para garantizar
    /// el efecto exactly-once ante el re-procesamiento del outbox (DEC-B5-05).
    /// </summary>
    /// <param name="userId">Identificador del usuario destinatario.</param>
    /// <param name="relatedEntityType">Tipo de la entidad relacionada.</param>
    /// <param name="relatedEntityId">Identificador de la entidad relacionada.</param>
    /// <param name="type">Tipo de notificación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si ya existe una notificación equivalente.</returns>
    Task<bool> ExistsForRelatedAsync(
        Guid userId,
        string relatedEntityType,
        Guid relatedEntityId,
        NotificationType type,
        CancellationToken ct = default);
}
