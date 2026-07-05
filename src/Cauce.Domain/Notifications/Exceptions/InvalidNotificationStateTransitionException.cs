using Cauce.Domain.Common.Exceptions;
using Cauce.Domain.Notifications.Enums;

namespace Cauce.Domain.Notifications.Exceptions;

/// <summary>
/// Se lanza cuando se intenta una transición de estado no permitida sobre una notificación.
/// </summary>
public sealed class InvalidNotificationStateTransitionException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con los estados de origen y destino.
    /// </summary>
    /// <param name="from">Estado de origen.</param>
    /// <param name="to">Estado de destino solicitado.</param>
    public InvalidNotificationStateTransitionException(NotificationStatus from, NotificationStatus to)
        : base($"Transición de estado de notificación inválida de '{from}' a '{to}'.")
    {
    }
}
