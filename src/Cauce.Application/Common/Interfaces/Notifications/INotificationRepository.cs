using Cauce.Domain.Notifications;

namespace Cauce.Application.Common.Interfaces.Notifications;

/// <summary>
/// Repositorio del agregado <see cref="Notification"/>.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Lista, con seguimiento de cambios, las notificaciones despachables (pendientes, vencidas y
    /// con reintentos disponibles), ordenadas por <c>scheduled_for</c> ascendente para preservar el
    /// orden de envío.
    /// </summary>
    /// <param name="now">Marca de tiempo UTC de referencia.</param>
    /// <param name="batchSize">Tamaño del lote.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las notificaciones a despachar.</returns>
    Task<IReadOnlyList<Notification>> ListDispatchableAsync(DateTime now, int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Agrega una notificación al contexto de persistencia (sin <c>SaveChanges</c>).
    /// </summary>
    /// <param name="notification">Notificación a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(Notification notification, CancellationToken ct = default);
}
