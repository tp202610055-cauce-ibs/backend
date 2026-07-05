using Cauce.Application.Common.Interfaces.Outbox;
using Cauce.Domain.Outbox;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Outbox;

/// <summary>
/// Implementación de <see cref="IOutboxRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class OutboxRepository : IOutboxRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public OutboxRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListPendingIdsAsync(DateTime now, int batchSize, CancellationToken ct = default)
    {
        return await _context.OutboxMessages
            .AsNoTracking()
            .Where(message => message.ProcessedAt == null
                && (message.NextAttemptAt == null || message.NextAttemptAt <= now))
            .OrderBy(message => message.OccurredAt)
            .Take(batchSize)
            .Select(message => message.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<OutboxMessage?> GetByIdAsync(Guid outboxId, CancellationToken ct = default)
    {
        return _context.OutboxMessages.FirstOrDefaultAsync(message => message.Id == outboxId, ct);
    }

    /// <inheritdoc />
    public Task<int> DeleteProcessedOlderThanAsync(DateTime processedBefore, CancellationToken ct = default)
    {
        return _context.OutboxMessages
            .Where(message => message.ProcessedAt != null && message.ProcessedAt < processedBefore)
            .ExecuteDeleteAsync(ct);
    }
}
