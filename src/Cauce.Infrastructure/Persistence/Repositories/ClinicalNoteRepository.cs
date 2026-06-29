using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IClinicalNoteRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class ClinicalNoteRepository : IClinicalNoteRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public ClinicalNoteRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<ClinicalNote?> FindByIdAsync(Guid noteId, CancellationToken ct = default)
    {
        return _context.Set<ClinicalNote>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == noteId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClinicalNote>> ListByPatientInRangeAsync(
        Guid patientId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _context.Set<ClinicalNote>()
            .AsNoTracking()
            .Where(x => x.PatientId == patientId && x.CreatedAt >= from && x.CreatedAt <= to)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(ClinicalNote note, CancellationToken ct = default)
    {
        await _context.Set<ClinicalNote>().AddAsync(note, ct).ConfigureAwait(false);
    }
}
