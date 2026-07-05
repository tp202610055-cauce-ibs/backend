using Cauce.Domain.Common;
using Cauce.Domain.Notifications.Enums;
using Cauce.Domain.Notifications.Exceptions;

namespace Cauce.Domain.Notifications;

/// <summary>
/// Notificación dirigida a un usuario a través de un canal (push o email). Gobierna su propia
/// máquina de estados y su reprogramación con backoff exponencial ante fallos de entrega.
/// </summary>
public sealed class Notification : Entity
{
    private const int MaxRetries = 3;
    private const int MaxTitleLength = 150;
    private const int MaxBodyLength = 10000;
    private const int MaxErrorLength = 2000;

    /// <summary>
    /// Identificador del usuario destinatario.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Tipo de la notificación.
    /// </summary>
    public NotificationType Type { get; private set; }

    /// <summary>
    /// Canal de entrega.
    /// </summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>
    /// Título de la notificación.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Cuerpo de la notificación.
    /// </summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// Estado actual del ciclo de entrega.
    /// </summary>
    public NotificationStatus Status { get; private set; }

    /// <summary>
    /// Cantidad de intentos de entrega fallidos.
    /// </summary>
    public short RetryCount { get; private set; }

    /// <summary>
    /// Tipo de la entidad relacionada (por ejemplo, <c>"recommendation"</c>), o <see langword="null"/>.
    /// </summary>
    public string? RelatedEntityType { get; private set; }

    /// <summary>
    /// Identificador de la entidad relacionada, o <see langword="null"/>.
    /// </summary>
    public Guid? RelatedEntityId { get; private set; }

    /// <summary>
    /// Momento, en UTC, a partir del cual la notificación puede enviarse.
    /// </summary>
    public DateTime ScheduledFor { get; private set; }

    /// <summary>
    /// Momento de envío al proveedor, en UTC; <see langword="null"/> si aún no se envió.
    /// </summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>
    /// Momento de confirmación de entrega, en UTC; <see langword="null"/> si no se confirmó.
    /// </summary>
    public DateTime? DeliveredAt { get; private set; }

    /// <summary>
    /// Último mensaje de error, o <see langword="null"/>.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    private Notification()
    {
    }

    private Notification(
        Guid id,
        Guid userId,
        NotificationType type,
        NotificationChannel channel,
        string title,
        string body,
        DateTime scheduledFor,
        string? relatedEntityType,
        Guid? relatedEntityId)
        : base(id)
    {
        UserId = userId;
        Type = type;
        Channel = channel;
        Title = title;
        Body = body;
        Status = NotificationStatus.Pending;
        RetryCount = 0;
        ScheduledFor = scheduledFor;
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
    }

    /// <summary>
    /// Agenda una nueva notificación en estado pendiente.
    /// </summary>
    /// <param name="userId">Identificador del usuario destinatario.</param>
    /// <param name="type">Tipo de notificación.</param>
    /// <param name="channel">Canal de entrega.</param>
    /// <param name="title">Título (máximo 150 caracteres).</param>
    /// <param name="body">Cuerpo (máximo 10000 caracteres).</param>
    /// <param name="scheduledFor">Momento UTC a partir del cual puede enviarse.</param>
    /// <param name="relatedEntityType">Tipo de la entidad relacionada, opcional.</param>
    /// <param name="relatedEntityId">Identificador de la entidad relacionada, opcional.</param>
    /// <returns>La nueva notificación.</returns>
    /// <exception cref="ArgumentException">Si el título o el cuerpo son inválidos.</exception>
    public static Notification Schedule(
        Guid userId,
        NotificationType type,
        NotificationChannel channel,
        string title,
        string body,
        DateTime scheduledFor,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > MaxTitleLength)
        {
            throw new ArgumentException($"El título es obligatorio y no puede superar los {MaxTitleLength} caracteres.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)
        {
            throw new ArgumentException($"El cuerpo es obligatorio y no puede superar los {MaxBodyLength} caracteres.", nameof(body));
        }

        return new Notification(
            Guid.NewGuid(), userId, type, channel, title, body, scheduledFor, relatedEntityType, relatedEntityId);
    }

    /// <summary>
    /// Marca la notificación como enviada al proveedor.
    /// </summary>
    /// <param name="sentAtUtc">Momento de envío, en UTC.</param>
    /// <exception cref="InvalidNotificationStateTransitionException">Si no está pendiente.</exception>
    public void MarkAsSent(DateTime sentAtUtc)
    {
        if (Status != NotificationStatus.Pending)
        {
            throw new InvalidNotificationStateTransitionException(Status, NotificationStatus.Sent);
        }

        Status = NotificationStatus.Sent;
        SentAt = sentAtUtc;
        ErrorMessage = null;
    }

    /// <summary>
    /// Marca la notificación como entregada, confirmada por el proveedor.
    /// </summary>
    /// <param name="deliveredAtUtc">Momento de entrega, en UTC.</param>
    /// <exception cref="InvalidNotificationStateTransitionException">Si no fue enviada.</exception>
    public void MarkAsDelivered(DateTime deliveredAtUtc)
    {
        if (Status != NotificationStatus.Sent)
        {
            throw new InvalidNotificationStateTransitionException(Status, NotificationStatus.Delivered);
        }

        Status = NotificationStatus.Delivered;
        DeliveredAt = deliveredAtUtc;
    }

    /// <summary>
    /// Registra un intento de entrega fallido. Incrementa el contador y, si aún quedan reintentos,
    /// reprograma con backoff exponencial (1 min, 5 min, 30 min). Al agotar los reintentos, marca la
    /// notificación como fallida.
    /// </summary>
    /// <param name="errorMessage">Mensaje de error del proveedor.</param>
    /// <param name="nowUtc">Marca de tiempo UTC del intento.</param>
    /// <exception cref="InvalidNotificationStateTransitionException">Si ya fue entregada o falló.</exception>
    public void RecordFailedAttempt(string errorMessage, DateTime nowUtc)
    {
        if (Status is NotificationStatus.Delivered or NotificationStatus.Failed)
        {
            throw new InvalidNotificationStateTransitionException(Status, Status);
        }

        RetryCount++;
        ErrorMessage = Truncate(errorMessage, MaxErrorLength);

        if (RetryCount >= MaxRetries)
        {
            Status = NotificationStatus.Failed;
        }
        else
        {
            Status = NotificationStatus.Pending;
            ScheduledFor = nowUtc + BackoffFor(RetryCount);
        }
    }

    /// <summary>
    /// Indica si la notificación puede intentarse ahora.
    /// </summary>
    /// <param name="nowUtc">Marca de tiempo UTC de referencia.</param>
    /// <returns><see langword="true"/> si está pendiente, vencida y con reintentos disponibles.</returns>
    public bool CanRetryNow(DateTime nowUtc)
    {
        return Status == NotificationStatus.Pending
            && ScheduledFor <= nowUtc
            && RetryCount < MaxRetries;
    }

    private static TimeSpan BackoffFor(int retryCount)
    {
        return retryCount switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(30)
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
