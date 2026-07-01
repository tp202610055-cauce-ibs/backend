using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Common.Models;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IRecommendationRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class RecommendationRepository : IRecommendationRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public RecommendationRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<Recommendation?> GetByIdAsync(Guid recommendationId, CancellationToken ct = default)
    {
        return _context.Set<Recommendation>().FirstOrDefaultAsync(x => x.Id == recommendationId, ct);
    }

    /// <inheritdoc />
    public Task<Recommendation?> GetByIdWithDetailsAsync(Guid recommendationId, CancellationToken ct = default)
    {
        return _context.Set<Recommendation>()
            .Include(x => x.Items)
            .Include(x => x.Feedback)
            .FirstOrDefaultAsync(x => x.Id == recommendationId, ct);
    }

    /// <inheritdoc />
    public async Task<PagedResult<Recommendation>> ListByPatientAsync(
        Guid patientId,
        RecommendationStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Set<Recommendation>()
            .Include(x => x.Items)
            .Where(x => x.PatientId == patientId);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(x => x.GeneratedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<Recommendation>(items, page, pageSize, total);
    }

    /// <inheritdoc />
    public async Task<PagedResult<Recommendation>> ListPendingReviewByNutritionistAsync(
        Guid nutritionistId,
        int page,
        int pageSize,
        DateTime now,
        CancellationToken ct = default)
    {
        var assignedPatientIds = _context.Set<NutritionistPatient>()
            .Where(assignment => assignment.NutritionistId == nutritionistId
                && assignment.Status == AssignmentStatus.Active)
            .Select(assignment => assignment.PatientId);

        // Solo lectura: excluye las expiradas pero no las transita a Expired; esa responsabilidad
        // es del worker de barrido del Prompt 5 (ver DEC-B4-06).
        var query = _context.Set<Recommendation>()
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.Status == RecommendationStatus.PendingReview
                && x.ExpiresAt != null
                && x.ExpiresAt > now
                && assignedPatientIds.Contains(x.PatientId));

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderBy(x => x.GeneratedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<Recommendation>(items, page, pageSize, total);
    }

    /// <inheritdoc />
    public async Task AddAsync(Recommendation recommendation, CancellationToken ct = default)
    {
        await _context.Set<Recommendation>().AddAsync(recommendation, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddFeedbackAsync(RecommendationFeedback feedback, CancellationToken ct = default)
    {
        await _context.Set<RecommendationFeedback>().AddAsync(feedback, ct).ConfigureAwait(false);
    }
}
