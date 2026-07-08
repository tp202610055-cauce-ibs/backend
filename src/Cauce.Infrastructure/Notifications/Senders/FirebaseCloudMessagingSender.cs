using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Infrastructure.Persistence;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainNotification = Cauce.Domain.Notifications.Notification;

namespace Cauce.Infrastructure.Notifications.Senders;

/// <summary>
/// Remitente de notificaciones push vía Firebase Cloud Messaging. Resuelve el token FCM del
/// dispositivo del usuario a partir de su identificador. Si el usuario no tiene token registrado,
/// devuelve un fallo controlado (el endpoint de registro de token es deuda técnica pre-piloto, acta A2).
/// </summary>
public sealed class FirebaseCloudMessagingSender : INotificationSender
{
    private readonly CauceDbContext _context;
    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FirebaseCloudMessagingSender> _logger;

    /// <summary>
    /// Inicializa el remitente con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos, para resolver el token del destinatario.</param>
    /// <param name="messaging">Cliente de Firebase Cloud Messaging.</param>
    /// <param name="logger">Logger de la categoría del remitente.</param>
    public FirebaseCloudMessagingSender(
        CauceDbContext context,
        FirebaseMessaging messaging,
        ILogger<FirebaseCloudMessagingSender> logger)
    {
        _context = context;
        _messaging = messaging;
        _logger = logger;
    }

    /// <inheritdoc />
    public Cauce.Domain.Notifications.Enums.NotificationChannel Channel =>
        Cauce.Domain.Notifications.Enums.NotificationChannel.Push;

    /// <inheritdoc />
    public async Task<NotificationSendResult> SendAsync(DomainNotification notification, CancellationToken ct = default)
    {
        var fcmToken = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == notification.UserId)
            .Select(user => user.FcmToken)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(fcmToken))
        {
            return new NotificationSendResult(false, null, "El usuario no tiene un token de dispositivo registrado.");
        }

        try
        {
            var message = BuildMessage(fcmToken, notification);
            var messageId = await _messaging.SendAsync(message, ct).ConfigureAwait(false);
            return new NotificationSendResult(true, messageId, null);
        }
        catch (FirebaseMessagingException exception)
        {
            // TS10 CA01: si el proveedor indica que el token ya no es válido, se desvincula del usuario
            // para dejar de reintentar contra un dispositivo inexistente.
            if (IsInvalidTokenError(exception.MessagingErrorCode))
            {
                await ClearInvalidTokenAsync(notification.UserId, exception.MessagingErrorCode, ct).ConfigureAwait(false);
                return new NotificationSendResult(false, null, "fcm_token_invalid_cleared");
            }

            _logger.LogWarning(exception, "FCM push notification {NotificationId} failed.", notification.Id);
            return new NotificationSendResult(false, null, exception.Message);
        }
    }

    /// <summary>
    /// Indica si el código de error de FCM implica que el token del dispositivo dejó de ser válido y,
    /// por tanto, debe desvincularse (TS10 CA01).
    /// </summary>
    /// <param name="errorCode">Código de error de FCM.</param>
    /// <returns><see langword="true"/> si el token es inválido y debe limpiarse.</returns>
    internal static bool IsInvalidTokenError(MessagingErrorCode? errorCode)
    {
        return errorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument;
    }

    /// <summary>
    /// Desvincula el token de FCM del usuario cuando el proveedor lo reporta como inválido, y persiste
    /// el cambio en su propio ámbito.
    /// </summary>
    /// <param name="userId">Identificador del usuario destinatario.</param>
    /// <param name="errorCode">Código de error de FCM que motivó la invalidación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    private async Task ClearInvalidTokenAsync(Guid userId, MessagingErrorCode? errorCode, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct).ConfigureAwait(false);
        if (user is not null)
        {
            user.RegisterFcmToken(null);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        _logger.LogWarning("FCM token invalidated for user {UserId} due to {ErrorCode}.", userId, errorCode);
    }

    /// <summary>
    /// Construye el mensaje de Firebase Cloud Messaging a partir del token del dispositivo y la
    /// notificación de dominio. Extraído para poder probar el mapeo dominio→payload sin red.
    /// </summary>
    /// <param name="fcmToken">Token del dispositivo destino.</param>
    /// <param name="notification">Notificación de dominio.</param>
    /// <returns>El mensaje de FCM.</returns>
    internal static Message BuildMessage(string fcmToken, DomainNotification notification)
    {
        return new Message
        {
            Token = fcmToken,
            Notification = new FirebaseAdmin.Messaging.Notification
            {
                Title = notification.Title,
                Body = notification.Body
            }
        };
    }
}
