namespace Cauce.Application.ClinicalRegistry.UseCases.SyncBatch;

/// <summary>
/// Resultado de la sincronización de un lote, agrupado en elementos aceptados (creados
/// ahora), duplicados (ya procesados con carga idéntica) y errores.
/// </summary>
/// <param name="Accepted">Elementos creados en esta sincronización.</param>
/// <param name="Duplicates">Elementos cuyo <c>client_guid</c> ya había sido procesado.</param>
/// <param name="Errors">Elementos que fallaron por validación, invariante o conflicto de idempotencia.</param>
public sealed record SyncBatchResult(
    IReadOnlyList<SyncAcceptedEntry> Accepted,
    IReadOnlyList<SyncDuplicateEntry> Duplicates,
    IReadOnlyList<SyncErrorEntry> Errors);

/// <summary>
/// Elemento aceptado del lote.
/// </summary>
/// <param name="ClientGuid">Identificador del dispositivo.</param>
/// <param name="ServerId">Identificador asignado por el servidor.</param>
/// <param name="EntityType">Tipo de entidad (<c>Meal</c> o <c>Symptom</c>).</param>
public sealed record SyncAcceptedEntry(Guid ClientGuid, Guid ServerId, string EntityType);

/// <summary>
/// Elemento duplicado del lote.
/// </summary>
/// <param name="ClientGuid">Identificador del dispositivo.</param>
/// <param name="ExistingServerId">Identificador del registro ya existente.</param>
public sealed record SyncDuplicateEntry(Guid ClientGuid, Guid ExistingServerId);

/// <summary>
/// Elemento del lote que falló.
/// </summary>
/// <param name="ClientGuid">Identificador del dispositivo.</param>
/// <param name="ErrorCode">Código de error legible por máquina.</param>
/// <param name="Message">Descripción del error.</param>
public sealed record SyncErrorEntry(Guid ClientGuid, string ErrorCode, string Message);
