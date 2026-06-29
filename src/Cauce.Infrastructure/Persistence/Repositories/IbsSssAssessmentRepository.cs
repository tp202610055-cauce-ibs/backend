using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IIbsSssAssessmentRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class IbsSssAssessmentRepository : IIbsSssAssessmentRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public IbsSssAssessmentRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<IbsSssAssessment?> FindByIdAsync(Guid assessmentId, CancellationToken ct = default)
    {
        return _context.Set<IbsSssAssessment>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == assessmentId, ct);
    }

    /// <inheritdoc />
    public Task<IbsSssAssessment?> FindBaselineByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        return _context.Set<IbsSssAssessment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PatientId == patientId && x.AssessmentType == AssessmentType.Baseline, ct);
    }

    /// <inheritdoc />
    public Task<IbsSssAssessment?> FindLatestByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        return _context.Set<IbsSssAssessment>()
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IbsSssAssessment>> ListByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        return await _context.Set<IbsSssAssessment>()
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderBy(x => x.CycleNumber)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> GetNextCycleNumberAsync(Guid patientId, CancellationToken ct = default)
    {
        var maxCycle = await _context.Set<IbsSssAssessment>()
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .MaxAsync(x => (int?)x.CycleNumber, ct)
            .ConfigureAwait(false);

        return (maxCycle ?? 0) + 1;
    }

    /// <inheritdoc />
    public async Task AddAsync(IbsSssAssessment assessment, CancellationToken ct = default)
    {
        await _context.Set<IbsSssAssessment>().AddAsync(assessment, ct).ConfigureAwait(false);
    }
}
