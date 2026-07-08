using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IIbsSssAssessmentScheduleRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class IbsSssAssessmentScheduleRepository : IIbsSssAssessmentScheduleRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public IbsSssAssessmentScheduleRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<IbsSssAssessmentSchedule?> FindOpenByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        return _context.IbsSssSchedules
            .Where(schedule => schedule.PatientId == patientId && !schedule.Completed && !schedule.Missed)
            .OrderByDescending(schedule => schedule.DueDate)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(IbsSssAssessmentSchedule schedule, CancellationToken ct = default)
    {
        await _context.IbsSssSchedules.AddAsync(schedule, ct).ConfigureAwait(false);
    }
}
