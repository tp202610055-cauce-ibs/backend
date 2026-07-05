using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;

namespace Cauce.Application.Common.Interfaces.Notifications;

/// <summary>
/// Proveedor de entrega de notificaciones por un canal específico (push o email).
/// </summary>
public interface INotificationSender
{
    /// <summary>
    /// Canal que atiende este proveedor.
    /// </summary>
    NotificationChannel Channel { get; }

    /// <summary>
    /// Envía la notificación por el canal del proveedor.
    /// </summary>
    /// <param name="notification">Notificación a enviar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El resultado del envío.</returns>
    Task<NotificationSendResult> SendAsync(Notification notification, CancellationToken ct = default);
}

/// <summary>
/// Resultado del envío de una notificación por un proveedor.
/// </summary>
/// <param name="Success">Indica si el envío fue exitoso.</param>
/// <param name="ProviderMessageId">Identificador del mensaje en el proveedor, o <see langword="null"/>.</param>
/// <param name="ErrorMessage">Mensaje de error, o <see langword="null"/> si fue exitoso.</param>
public sealed record NotificationSendResult(bool Success, string? ProviderMessageId, string? ErrorMessage);
