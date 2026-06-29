using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="ISymptomRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class SymptomRepository : ISymptomRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public SymptomRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<Symptom?> FindByIdAsync(Guid symptomId, CancellationToken ct = default)
    {
        return _context.Set<Symptom>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == symptomId, ct);
    }

    /// <inheritdoc />
    public Task<Symptom?> FindByClientGuidAsync(Guid clientGuid, CancellationToken ct = default)
    {
        return _context.Set<Symptom>().AsNoTracking().FirstOrDefaultAsync(x => x.ClientGuid == clientGuid, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Symptom>> ListByPatientInRangeAsync(
        Guid patientId, DateTime from, DateTime to, int skip, int take, CancellationToken ct = default)
    {
        return await _context.Set<Symptom>()
            .AsNoTracking()
            .Where(x => x.PatientId == patientId && x.ClientCreatedAt >= from && x.ClientCreatedAt <= to)
            .OrderByDescending(x => x.ClientCreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<int> CountByPatientInRangeAsync(Guid patientId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return _context.Set<Symptom>()
            .AsNoTracking()
            .CountAsync(x => x.PatientId == patientId && x.ClientCreatedAt >= from && x.ClientCreatedAt <= to, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(Symptom symptom, CancellationToken ct = default)
    {
        await _context.Set<Symptom>().AddAsync(symptom, ct).ConfigureAwait(false);
    }
}
