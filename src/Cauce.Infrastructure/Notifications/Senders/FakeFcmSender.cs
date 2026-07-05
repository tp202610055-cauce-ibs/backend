using System.Collections.Concurrent;
using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Notifications.Senders;

/// <summary>
/// Remitente falso de notificaciones push, para desarrollo y pruebas. No realiza ninguna llamada de
/// red: registra cada envío en <see cref="SentMessages"/> (observable por las pruebas) y devuelve
/// éxito, salvo que se haya configurado un fallo puntual con <see cref="SimulateFailureNextCall"/>.
/// Se selecciona cuando <c>Notifications:Fcm:UseFake</c> es <see langword="true"/> (acta A2).
/// </summary>
public sealed class FakeFcmSender : INotificationSender
{
    private readonly ConcurrentQueue<SentPush> _sent = new();
    private readonly ILogger<FakeFcmSender> _logger;
    private string? _forcedFailure;

    /// <summary>
    /// Inicializa el remitente falso con su logger.
    /// </summary>
    /// <param name="logger">Logger de la categoría del remitente.</param>
    public FakeFcmSender(ILogger<FakeFcmSender> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registro de un push simulado, para aserciones en pruebas.
    /// </summary>
    /// <param name="UserId">Destinatario.</param>
    /// <param name="Title">Título.</param>
    /// <param name="Body">Cuerpo.</param>
    /// <param name="NotificationId">Identificador de la notificación.</param>
    public sealed record SentPush(Guid UserId, string Title, string Body, Guid NotificationId);

    /// <summary>
    /// Mensajes push simulados durante la ejecución, en orden de envío.
    /// </summary>
    public IReadOnlyCollection<SentPush> SentMessages => _sent.ToArray();

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.Push;

    /// <summary>
    /// Configura que la próxima llamada a <see cref="SendAsync"/> falle con el error indicado. El
    /// fallo se consume una sola vez.
    /// </summary>
    /// <param name="error">Mensaje de error a devolver.</param>
    public void SimulateFailureNextCall(string error)
    {
        _forcedFailure = error;
    }

    /// <summary>
    /// Reinicia el estado observable (mensajes registrados y fallo forzado).
    /// </summary>
    public void Reset()
    {
        _sent.Clear();
        _forcedFailure = null;
    }

    /// <inheritdoc />
    public Task<NotificationSendResult> SendAsync(Notification notification, CancellationToken ct = default)
    {
        if (_forcedFailure is { } error)
        {
            _forcedFailure = null;
            _logger.LogInformation(
                "[FakeFcm] Simulated failure for push notification {NotificationId}.", notification.Id);
            return Task.FromResult(new NotificationSendResult(false, null, error));
        }

        _sent.Enqueue(new SentPush(notification.UserId, notification.Title, notification.Body, notification.Id));
        _logger.LogInformation(
            "[FakeFcm] Push notification {NotificationId} for user {UserId} simulated as delivered.",
            notification.Id,
            notification.UserId);
        return Task.FromResult(new NotificationSendResult(true, $"fake-{notification.Id}", null));
    }
}
