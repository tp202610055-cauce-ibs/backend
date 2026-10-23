using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Infrastructure.Persistence;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainNotification = Cauce.Domain.Notifications.Notification;

namespace Cauce.Infrastructure.Notifications.Senders;

/// <summary>
/// Remitente de notificaciones push vía Firebase Cloud Messaging. Resuelve el token FCM del
/// dispositivo del usuario a partir de su identificador; sin token o sin Firebase, el envío falla controlado.
/// </summary>
public sealed class FirebaseCloudMessagingSender : INotificationSender
{
    /// <summary>
    /// Canal de Android en el que la app muestra las notificaciones; lo crea la app al iniciar.
    /// </summary>
    internal const string AndroidChannelId = "cauce_default";

    /// <summary>
    /// Error registrado cuando Firebase no se pudo inicializar con la credencial configurada.
    /// </summary>
    internal const string UnavailableError = "fcm_unavailable";

    private readonly CauceDbContext _context;
    private readonly FirebaseMessagingProvider _messagingProvider;
    private readonly ILogger<FirebaseCloudMessagingSender> _logger;

    /// <summary>
    /// Inicializa el remitente con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos, para resolver el token del destinatario.</param>
    /// <param name="messagingProvider">Proveedor del cliente de Firebase Cloud Messaging.</param>
    /// <param name="logger">Logger de la categoría del remitente.</param>
    public FirebaseCloudMessagingSender(
        CauceDbContext context,
        FirebaseMessagingProvider messagingProvider,
        ILogger<FirebaseCloudMessagingSender> logger)
    {
        _context = context;
        _messagingProvider = messagingProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Cauce.Domain.Notifications.Enums.NotificationChannel Channel =>
        Cauce.Domain.Notifications.Enums.NotificationChannel.Push;

    /// <inheritdoc />
    public async Task<NotificationSendResult> SendAsync(DomainNotification notification, CancellationToken ct = default)
    {
        var messaging = _messagingProvider.Messaging;
        if (messaging is null)
        {
            return new NotificationSendResult(false, null, UnavailableError);
        }

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
            var messageId = await messaging.SendAsync(message, ct).ConfigureAwait(false);
            return new NotificationSendResult(true, messageId, null);
        }
        catch (FirebaseMessagingException exception)
        {
            // TS10 CA01: un token que el proveedor ya no reconoce se desvincula para no reintentar en vano.
            if (IsInvalidTokenError(exception.MessagingErrorCode, exception.Message))
            {
                await ClearInvalidTokenAsync(notification.UserId, exception.MessagingErrorCode, ct).ConfigureAwait(false);
                return new NotificationSendResult(false, null, "fcm_token_invalid_cleared");
            }

            _logger.LogWarning(exception, "FCM push notification {NotificationId} failed.", notification.Id);
            return new NotificationSendResult(false, null, exception.Message);
        }
    }

    /// <summary>
    /// Indica si el error de FCM significa que el token del dispositivo dejó de ser válido. Un
    /// <c>InvalidArgument</c> solo cuenta si habla del token: un mensaje mal formado no borra tokens sanos.
    /// </summary>
    /// <param name="errorCode">Código de error de FCM.</param>
    /// <param name="errorMessage">Mensaje de error de FCM.</param>
    internal static bool IsInvalidTokenError(MessagingErrorCode? errorCode, string? errorMessage)
    {
        return errorCode switch
        {
            MessagingErrorCode.Unregistered => true,
            MessagingErrorCode.InvalidArgument =>
                errorMessage?.Contains("registration token", StringComparison.OrdinalIgnoreCase) == true,
            _ => false
        };
    }

    /// <summary>
    /// Desvincula el token de FCM del usuario cuando el proveedor lo reporta como inválido, y persiste
    /// el cambio en su propio ámbito.
    /// </summary>
    /// <param name="userId">Identificador del usuario destinatario.</param>
    /// <param name="errorCode">Código de error de FCM que motivó la invalidación.</param>
    /// <param name="ct">Token de cancelación.</param>
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
    /// Construye el mensaje de FCM: el bloque visible, los datos con los que la app abre la pantalla
    /// correcta y la configuración de Android. Se prueba sin red.
    /// </summary>
    /// <param name="fcmToken">Token del dispositivo destino.</param>
    /// <param name="notification">Notificación de dominio.</param>
    internal static Message BuildMessage(string fcmToken, DomainNotification notification)
    {
        return new Message
        {
            Token = fcmToken,
            Notification = new FirebaseAdmin.Messaging.Notification
            {
                Title = notification.Title,
                Body = notification.Body
            },
            Data = new Dictionary<string, string>
            {
                ["type"] = notification.Type.ToString().ToLowerInvariant(),
                ["entityType"] = notification.RelatedEntityType ?? string.Empty,
                ["entityId"] = notification.RelatedEntityId?.ToString() ?? string.Empty
            },
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification { ChannelId = AndroidChannelId }
            }
        };
    }
}
