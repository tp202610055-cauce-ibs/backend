namespace Cauce.Domain.Notifications.Enums;

/// <summary>
/// Estado de una notificación en su ciclo de entrega. En base de datos se persiste como
/// <c>varchar</c> en snake_case.
/// </summary>
public enum NotificationStatus
{
    /// <summary>
    /// Pendiente de envío.
    /// </summary>
    Pending,

    /// <summary>
    /// Enviada al proveedor.
    /// </summary>
    Sent,

    /// <summary>
    /// Entrega confirmada por el proveedor.
    /// </summary>
    Delivered,

    /// <summary>
    /// Fallida tras agotar los reintentos.
    /// </summary>
    Failed
}
