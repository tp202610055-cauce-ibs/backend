namespace Cauce.Domain.ClinicalRegistry.Enums;

/// <summary>
/// Estado de sincronización de un registro generado en el dispositivo móvil
/// (offline-first). En base de datos se persiste como <c>varchar</c> en snake_case
/// lowercase (<c>sync_pending</c>, <c>sync_completed</c>).
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// El registro existe localmente y aún no se confirmó su sincronización con el servidor.
    /// </summary>
    SyncPending = 0,

    /// <summary>
    /// El registro fue sincronizado con el servidor.
    /// </summary>
    SyncCompleted = 1
}
