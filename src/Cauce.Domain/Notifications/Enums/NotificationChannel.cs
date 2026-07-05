namespace Cauce.Domain.Notifications.Enums;

/// <summary>
/// Canal de entrega de una notificación. En base de datos se persiste como <c>varchar</c> en
/// snake_case.
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// Notificación push a la aplicación móvil (Firebase Cloud Messaging).
    /// </summary>
    Push,

    /// <summary>
    /// Correo electrónico (SMTP).
    /// </summary>
    Email
}
