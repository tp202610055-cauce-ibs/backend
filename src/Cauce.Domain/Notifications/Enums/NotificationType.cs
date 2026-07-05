namespace Cauce.Domain.Notifications.Enums;

/// <summary>
/// Tipo de una notificación. En base de datos se persiste como <c>varchar</c> en snake_case.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Relacionada con una recomendación (generada, aprobada, etc.).
    /// </summary>
    Recommendation,

    /// <summary>
    /// Recordatorio (por ejemplo, el recordatorio semanal de recomendación).
    /// </summary>
    Reminder,

    /// <summary>
    /// Alerta clínica u operativa.
    /// </summary>
    Alert,

    /// <summary>
    /// Notificación del sistema (por ejemplo, disponibilidad de un reporte).
    /// </summary>
    System
}
