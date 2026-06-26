using Cauce.Domain.Auditing.Enums;

namespace Cauce.Application.Common.Interfaces;

/// <summary>
/// Servicio para escribir eventos en la bitácora de auditoría desde los handlers
/// de la capa de aplicación. La implementación completa los datos del actor
/// (usuario, IP, user agent) y el instante del evento a partir del contexto actual.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Registra un evento de auditoría.
    /// </summary>
    /// <param name="actionType">Tipo de acción auditada.</param>
    /// <param name="entityType">Nombre del tipo de entidad afectada.</param>
    /// <param name="entityId">Identificador del registro afectado, o <see langword="null"/> si no aplica.</param>
    /// <param name="oldValuesHash">Hash SHA-256 del estado previo, o <see langword="null"/> en creaciones.</param>
    /// <param name="newValuesHash">Hash SHA-256 del estado nuevo, o <see langword="null"/> en eliminaciones.</param>
    /// <param name="additionalContext">Contexto adicional en JSON, o <see langword="null"/> si no aplica.</param>
    /// <param name="cancellationToken">Token de cancelación de la operación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task LogAsync(
        AuditActionType actionType,
        string entityType,
        Guid? entityId,
        string? oldValuesHash,
        string? newValuesHash,
        string? additionalContext,
        CancellationToken cancellationToken = default);
}
