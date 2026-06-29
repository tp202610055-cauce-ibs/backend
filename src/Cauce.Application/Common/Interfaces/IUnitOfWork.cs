namespace Cauce.Application.Common.Interfaces;

/// <summary>
/// Abstracción de la unidad de trabajo. Confirma de forma atómica todos los
/// cambios pendientes acumulados durante la petición. La implementación envuelve
/// el <c>DbContext</c> de EF Core, que actúa como unidad de trabajo.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persiste todos los cambios pendientes en el almacén subyacente.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la operación.</param>
    /// <returns>Número de registros afectados.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarta los cambios rastreados aún no confirmados. Se usa para limpiar el
    /// rastreador tras un fallo de confirmación (por ejemplo, una violación de
    /// restricción única) y poder continuar procesando elementos independientes de un
    /// lote sin reintentar la entidad fallida.
    /// </summary>
    void DiscardTrackedChanges();
}
