using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Domain.Common;
using Cauce.Domain.Outbox;
using Cauce.Infrastructure.Persistence;

namespace Cauce.Infrastructure.Outbox;

/// <summary>
/// Implementación de <see cref="IOutboxWriter"/> que enrola un <see cref="OutboxMessage"/> en el
/// <c>ChangeTracker</c> del contexto, sin llamar a <c>SaveChangesAsync</c>: el <c>UnitOfWork</c> del
/// handler cierra la transacción, de modo que el evento se persiste en la misma transacción que el
/// cambio de negocio (patrón outbox transaccional, DEC-B5-04).
/// </summary>
public sealed class OutboxWriter : IOutboxWriter
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el escritor con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public OutboxWriter(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        Guid aggregateId,
        string aggregateType,
        IDomainEvent domainEvent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var eventType = domainEvent.GetType();
        var payloadJson = JsonSerializer.Serialize(domainEvent, eventType, OutboxSerialization.Options);

        var message = OutboxMessage.Publish(
            aggregateId,
            aggregateType,
            eventType.FullName ?? eventType.Name,
            payloadJson,
            domainEvent.OccurredOn);

        await _context.OutboxMessages.AddAsync(message, ct).ConfigureAwait(false);
    }
}
