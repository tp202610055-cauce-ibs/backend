using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="INutritionistPatientRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class NutritionistPatientRepository : INutritionistPatientRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public NutritionistPatientRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<NutritionistPatient?> FindActiveByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        return _context.Set<NutritionistPatient>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PatientId == patientId && x.Status == AssignmentStatus.Active, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NutritionistPatient>> ListActiveByNutritionistAsync(Guid nutritionistId, CancellationToken ct = default)
    {
        return await _context.Set<NutritionistPatient>()
            .AsNoTracking()
            .Where(x => x.NutritionistId == nutritionistId && x.Status == AssignmentStatus.Active)
            .OrderByDescending(x => x.AssignedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssignedPatientSummary>> ListAssignedPatientSummariesAsync(Guid nutritionistId, CancellationToken ct = default)
    {
        var query =
            from assignment in _context.Set<NutritionistPatient>().AsNoTracking()
            where assignment.NutritionistId == nutritionistId && assignment.Status == AssignmentStatus.Active
            join user in _context.Set<User>().AsNoTracking() on assignment.PatientId equals user.Id
            join profile in _context.Set<PatientProfile>().AsNoTracking() on user.Id equals profile.UserId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            orderby assignment.AssignedAt descending
            select new AssignedPatientSummary(
                user.Id,
                user.FullName,
                assignment.Id,
                assignment.AssignedAt,
                profile != null && profile.OnboardingCompleted,
                profile != null ? profile.IbsSubtype : (IbsSubtype?)null);

        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> ActiveAssignmentExistsAsync(Guid nutritionistId, Guid patientId, CancellationToken ct = default)
    {
        return _context.Set<NutritionistPatient>()
            .AsNoTracking()
            .AnyAsync(
                x => x.NutritionistId == nutritionistId
                    && x.PatientId == patientId
                    && x.Status == AssignmentStatus.Active,
                ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(NutritionistPatient assignment, CancellationToken ct = default)
    {
        await _context.Set<NutritionistPatient>().AddAsync(assignment, ct).ConfigureAwait(false);
    }
}
