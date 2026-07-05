using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Notifications.Exceptions;

/// <summary>
/// Se lanza cuando la entrega de una notificación falla de forma irrecuperable.
/// </summary>
public sealed class NotificationDeliveryFailedException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el motivo de la falla.
    /// </summary>
    /// <param name="reason">Descripción del fallo.</param>
    public NotificationDeliveryFailedException(string reason)
        : base($"La entrega de la notificación falló: {reason}")
    {
    }
}
