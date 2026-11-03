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
        // US14 CA03: el paciente solo ve recomendaciones activas en estados visibles (acta A24).
        var query = _context.Set<Recommendation>()
            .Include(x => x.Items)
            .Where(x => x.PatientId == patientId
                && x.IsActive
                && Recommendation.PatientVisibleStatuses.Contains(x.Status));

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
    public async Task<PagedResult<Recommendation>> ListAllByPatientAsync(
        Guid patientId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Set<Recommendation>()
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.PatientId == patientId);

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(x => x.GeneratedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<Recommendation>(items, page, pageSize, total);
    }

    /// <inheritdoc />
    public Task<bool> ExistsInReviewForPatientAsync(Guid patientId, DateTime now, CancellationToken ct = default)
    {
        // Mismo criterio de vencimiento que Recommendation.IsExpired: sin fecha nunca vence.
        return _context.Set<Recommendation>()
            .AsNoTracking()
            .AnyAsync(x => x.PatientId == patientId
                && x.IsActive
                && x.Status == RecommendationStatus.PendingReview
                && (x.ExpiresAt == null || x.ExpiresAt >= now), ct);
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

    /// <inheritdoc />
    public void Update(Recommendation recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        // Un ítem nuevo trae su clave desde el dominio: sin marcarlo como alta, EF lo toma por una fila
        // existente, emite un UPDATE que no afecta filas y el guardado falla por concurrencia.
        foreach (var item in recommendation.Items)
        {
            if (_context.Entry(item).State == EntityState.Detached)
            {
                _context.Add(item);
            }
        }
    }
}
