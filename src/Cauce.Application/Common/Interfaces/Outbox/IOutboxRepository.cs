using Cauce.Domain.Outbox;

namespace Cauce.Application.Common.Interfaces.Outbox;

/// <summary>
/// Repositorio del agregado <see cref="OutboxMessage"/>.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Lista los identificadores de los mensajes pendientes (sin procesar) cuya ventana de backoff ya
    /// venció (<c>next_attempt_at</c> nulo o menor o igual a <paramref name="now"/>), ordenados por
    /// ocurrencia, hasta el tamaño de lote indicado. Respeta el backoff exponencial ante fallos.
    /// </summary>
    /// <param name="now">Marca de tiempo UTC de referencia.</param>
    /// <param name="batchSize">Tamaño del lote.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los identificadores de los mensajes pendientes elegibles.</returns>
    Task<IReadOnlyList<Guid>> ListPendingIdsAsync(DateTime now, int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un mensaje por su identificador, con seguimiento de cambios.
    /// </summary>
    /// <param name="outboxId">Identificador del mensaje.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El mensaje o <see langword="null"/> si no existe.</returns>
    Task<OutboxMessage?> GetByIdAsync(Guid outboxId, CancellationToken ct = default);

    /// <summary>
    /// Elimina los mensajes procesados anteriores a la fecha indicada.
    /// </summary>
    /// <param name="processedBefore">Fecha de corte, en UTC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La cantidad de mensajes eliminados.</returns>
    Task<int> DeleteProcessedOlderThanAsync(DateTime processedBefore, CancellationToken ct = default);
}
