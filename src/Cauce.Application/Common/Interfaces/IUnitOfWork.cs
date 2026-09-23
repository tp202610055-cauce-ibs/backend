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

    /// <summary>
    /// Ejecuta una operación dentro de una única transacción explícita del almacén subyacente.
    ///
    /// <para>Se usa cuando un caso de uso necesita más de un <c>SaveChanges</c> y esos guardados
    /// tienen que ser atómicos entre sí. Si el almacén ya está dentro de una transacción (por ejemplo,
    /// porque un caso de uso externo la abrió), la operación se ejecuta tal cual, sin anidar.</para>
    /// </summary>
    /// <typeparam name="TResult">Tipo del resultado de la operación.</typeparam>
    /// <param name="operation">Operación a ejecutar dentro de la transacción.</param>
    /// <param name="cancellationToken">Token de cancelación de la operación.</param>
    /// <returns>El resultado de la operación.</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
