using Cauce.Domain.Common;

namespace Cauce.Domain.Outbox;

/// <summary>
/// Mensaje del patrón outbox transaccional: un evento de dominio persistido en la misma
/// transacción que el cambio de negocio que lo produjo, para su publicación asíncrona confiable.
/// </summary>
public sealed class OutboxMessage : Entity
{
    private const int DefaultMaxAttempts = 10;
    private const int MaxRetryDelaySeconds = 3600;
    private const int MaxErrorLength = 4000;

    /// <summary>
    /// Identificador del agregado que originó el evento.
    /// </summary>
    public Guid AggregateId { get; private set; }

    /// <summary>
    /// Tipo del agregado (por ejemplo, <c>"recommendation"</c>).
    /// </summary>
    public string AggregateType { get; private set; } = string.Empty;

    /// <summary>
    /// Tipo del evento (nombre del tipo del evento de dominio).
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// Carga del evento serializada como JSON.
    /// </summary>
    public string PayloadJson { get; private set; } = string.Empty;

    /// <summary>
    /// Momento de ocurrencia del evento, en UTC.
    /// </summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>
    /// Momento de procesamiento, en UTC; <see langword="null"/> si aún no se procesó.
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>
    /// Cantidad de intentos de procesamiento fallidos.
    /// </summary>
    public short Attempts { get; private set; }

    /// <summary>
    /// Momento, en UTC, a partir del cual el mensaje puede reintentarse tras un fallo (backoff
    /// exponencial); <see langword="null"/> si aún no ha fallado. El despachador excluye los mensajes
    /// cuyo <see cref="NextAttemptAt"/> es futuro, de modo que el backoff no solo se calcula sino que
    /// se respeta.
    /// </summary>
    public DateTime? NextAttemptAt { get; private set; }

    /// <summary>
    /// Último error de procesamiento, o <see langword="null"/>.
    /// </summary>
    public string? LastError { get; private set; }

    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        Guid aggregateId,
        string aggregateType,
        string eventType,
        string payloadJson,
        DateTime occurredAt)
        : base(id)
    {
        AggregateId = aggregateId;
        AggregateType = aggregateType;
        EventType = eventType;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        ProcessedAt = null;
        Attempts = 0;
        LastError = null;
    }

    /// <summary>
    /// Publica un nuevo mensaje de outbox.
    /// </summary>
    /// <param name="aggregateId">Identificador del agregado.</param>
    /// <param name="aggregateType">Tipo del agregado.</param>
    /// <param name="eventType">Tipo del evento.</param>
    /// <param name="payloadJson">Carga serializada.</param>
    /// <param name="occurredAtUtc">Momento de ocurrencia, en UTC.</param>
    /// <returns>El nuevo mensaje de outbox.</returns>
    /// <exception cref="ArgumentException">Si el tipo de agregado o de evento son vacíos.</exception>
    public static OutboxMessage Publish(
        Guid aggregateId,
        string aggregateType,
        string eventType,
        string payloadJson,
        DateTime occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        return new OutboxMessage(Guid.NewGuid(), aggregateId, aggregateType, eventType, payloadJson, occurredAtUtc);
    }

    /// <summary>
    /// Marca el mensaje como procesado con éxito.
    /// </summary>
    /// <param name="nowUtc">Marca de tiempo UTC.</param>
    public void MarkAsProcessed(DateTime nowUtc)
    {
        ProcessedAt = nowUtc;
        NextAttemptAt = null;
        LastError = null;
    }

    /// <summary>
    /// Registra un intento de procesamiento fallido. Al alcanzar el máximo de intentos, marca el
    /// mensaje como procesado ("envenenado") para sacarlo de la cola; requiere intervención manual.
    /// </summary>
    /// <param name="error">Mensaje de error.</param>
    /// <param name="nowUtc">Marca de tiempo UTC.</param>
    /// <param name="maxAttempts">Cantidad máxima de intentos.</param>
    /// <returns><see langword="true"/> si el mensaje quedó envenenado.</returns>
    public bool RecordFailedAttempt(string error, DateTime nowUtc, short maxAttempts = DefaultMaxAttempts)
    {
        Attempts++;
        LastError = Truncate(error, MaxErrorLength);

        if (Attempts >= maxAttempts)
        {
            ProcessedAt = nowUtc;
            NextAttemptAt = null;
            return true;
        }

        NextAttemptAt = nowUtc + NextRetryDelay();
        return false;
    }

    /// <summary>
    /// Calcula el retardo hasta el próximo reintento: <c>2^Attempts</c> segundos con techo de 3600.
    /// </summary>
    /// <returns>El retardo hasta el próximo reintento.</returns>
    public TimeSpan NextRetryDelay()
    {
        var seconds = Math.Min((int)Math.Pow(2, Attempts), MaxRetryDelaySeconds);
        return TimeSpan.FromSeconds(seconds);
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
