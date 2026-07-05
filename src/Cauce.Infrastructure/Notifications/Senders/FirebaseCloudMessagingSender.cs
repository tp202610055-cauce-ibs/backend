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
            _logger.LogWarning(exception, "FCM push notification {NotificationId} failed.", notification.Id);
            return new NotificationSendResult(false, null, exception.Message);
        }
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
