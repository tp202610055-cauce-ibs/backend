using Cauce.Domain.Common;

namespace Cauce.Application.Common.Interfaces;

/// <summary>
/// Escribe eventos de dominio en la tabla <c>outbox_messages</c> dentro de la misma transacción
/// que el cambio de negocio que los produjo (patrón outbox, DEC-B5-04). No llama
/// <c>SaveChangesAsync</c>: el <c>UnitOfWork</c> del handler cierra la transacción.
/// </summary>
public interface IOutboxWriter
{
    /// <summary>
    /// Encola un evento de dominio para su publicación asíncrona.
    /// </summary>
    /// <param name="aggregateId">Identificador del agregado que originó el evento.</param>
    /// <param name="aggregateType">Tipo del agregado.</param>
    /// <param name="domainEvent">Evento de dominio a encolar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task PublishAsync(Guid aggregateId, string aggregateType, IDomainEvent domainEvent, CancellationToken ct = default);
}
