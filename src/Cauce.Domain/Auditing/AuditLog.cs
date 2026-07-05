using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Common;

namespace Cauce.Domain.Auditing;

/// <summary>
/// Registro inmutable de un evento de auditoría. Es transversal a todos los
/// módulos y satisface el principio de no repudio exigido por la Ley N° 29733.
/// Una vez creado no puede modificarse ni eliminarse: la tabla <c>audit_logs</c>
/// tiene triggers de base de datos que bloquean UPDATE y DELETE.
/// </summary>
public sealed class AuditLog : Entity
{
    /// <summary>
    /// Identificador del usuario que ejecutó la acción. Es <see langword="null"/>
    /// cuando el actor es el sistema (por ejemplo, un worker en segundo plano).
    /// </summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>
    /// Tipo de acción auditada.
    /// </summary>
    public AuditActionType ActionType { get; private set; }

    /// <summary>
    /// Nombre del tipo de entidad afectada (por ejemplo, <c>"PatientProfile"</c>).
    /// </summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>
    /// Identificador del registro afectado. Es <see langword="null"/> cuando la
    /// acción no se refiere a una entidad concreta (por ejemplo, un login).
    /// </summary>
    public Guid? EntityId { get; private set; }

    /// <summary>
    /// Hash SHA-256 (hex) del estado previo serializado. Es <see langword="null"/>
    /// en operaciones de creación.
    /// </summary>
    public string? OldValuesHash { get; private set; }

    /// <summary>
    /// Hash SHA-256 (hex) del estado nuevo serializado. Es <see langword="null"/>
    /// en operaciones de eliminación.
    /// </summary>
    public string? NewValuesHash { get; private set; }

    /// <summary>
    /// Dirección IP de origen del actor. Es <see langword="null"/> si el evento
    /// es interno y no proviene de una petición HTTP.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// User agent del actor. Es <see langword="null"/> si el evento es interno.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Contexto adicional en formato JSON (por ejemplo, <c>reviewed_by</c> o
    /// <c>notes</c> en flujos HITL). Es <see langword="null"/> si no aplica.
    /// </summary>
    public string? AdditionalContext { get; private set; }

    /// <summary>
    /// Momento, en UTC, en que ocurrió el evento.
    /// </summary>
    public DateTime OccurredAt { get; private set; }

    private AuditLog()
    {
    }

    private AuditLog(
        Guid id,
        Guid? actorUserId,
        AuditActionType actionType,
        string entityType,
        Guid? entityId,
        string? oldValuesHash,
        string? newValuesHash,
        string? ipAddress,
        string? userAgent,
        string? additionalContext,
        DateTime occurredAt)
        : base(id)
    {
        ActorUserId = actorUserId;
        ActionType = actionType;
        EntityType = entityType;
        EntityId = entityId;
        OldValuesHash = oldValuesHash;
        NewValuesHash = newValuesHash;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        AdditionalContext = additionalContext;
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// Crea un nuevo registro de auditoría, validando sus invariantes. Es la única forma de
    /// construir un <see cref="AuditLog"/> desde el código de aplicación e infraestructura.
    /// </summary>
    /// <param name="actorUserId">Usuario que ejecutó la acción, o <see langword="null"/> si fue el sistema.</param>
    /// <param name="actionType">Tipo de acción auditada.</param>
    /// <param name="entityType">Nombre del tipo de entidad afectada.</param>
    /// <param name="entityId">Identificador del registro afectado, o <see langword="null"/> si no aplica.</param>
    /// <param name="oldValuesHash">Hash SHA-256 del estado previo, o <see langword="null"/> en creaciones.</param>
    /// <param name="newValuesHash">Hash SHA-256 del estado nuevo, o <see langword="null"/> en eliminaciones.</param>
    /// <param name="ipAddress">Dirección IP del actor, o <see langword="null"/> en eventos internos.</param>
    /// <param name="userAgent">User agent del actor, o <see langword="null"/> en eventos internos.</param>
    /// <param name="additionalContext">Contexto adicional en JSON, o <see langword="null"/> si no aplica.</param>
    /// <param name="occurredAtUtc">Momento UTC en que ocurrió el evento.</param>
    /// <returns>El nuevo registro de auditoría.</returns>
    /// <exception cref="ArgumentException">Si el tipo de entidad es vacío.</exception>
    public static AuditLog Record(
        Guid? actorUserId,
        AuditActionType actionType,
        string entityType,
        Guid? entityId,
        string? oldValuesHash,
        string? newValuesHash,
        string? ipAddress,
        string? userAgent,
        string? additionalContext,
        DateTime occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("El tipo de entidad es obligatorio.", nameof(entityType));
        }

        return new AuditLog(
            Guid.NewGuid(), actorUserId, actionType, entityType, entityId,
            oldValuesHash, newValuesHash, ipAddress, userAgent, additionalContext, occurredAtUtc);
    }

    /// <summary>
    /// Verifica la integridad forense del registro comparando su hash de valores nuevos con el
    /// esperado.
    /// </summary>
    /// <param name="expectedHash">Hash esperado.</param>
    /// <returns><see langword="true"/> si coinciden.</returns>
    public bool VerifyIntegrity(string expectedHash)
    {
        return string.Equals(NewValuesHash, expectedHash, StringComparison.Ordinal);
    }
}
