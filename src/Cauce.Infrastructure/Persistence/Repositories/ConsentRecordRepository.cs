using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IConsentRecordRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class ConsentRecordRepository : IConsentRecordRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public ConsentRecordRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<ConsentRecord?> FindCurrentForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return _context.Set<ConsentRecord>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IsCurrent, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(ConsentRecord record, CancellationToken ct = default)
    {
        await _context.Set<ConsentRecord>().AddAsync(record, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SupersedeCurrentAsync(Guid userId, CancellationToken ct = default)
    {
        var current = await _context.Set<ConsentRecord>()
            .Where(x => x.UserId == userId && x.IsCurrent)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var record in current)
        {
            record.Supersede();
        }
    }
}
