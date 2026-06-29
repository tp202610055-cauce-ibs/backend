using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IMealRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class MealRepository : IMealRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public MealRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<Meal?> FindByIdAsync(Guid mealId, CancellationToken ct = default)
    {
        return _context.Set<Meal>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == mealId, ct);
    }

    /// <inheritdoc />
    public Task<Meal?> FindByIdWithItemsAsync(Guid mealId, CancellationToken ct = default)
    {
        return _context.Set<Meal>()
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == mealId, ct);
    }

    /// <inheritdoc />
    public Task<Meal?> FindByClientGuidAsync(Guid clientGuid, CancellationToken ct = default)
    {
        return _context.Set<Meal>().AsNoTracking().FirstOrDefaultAsync(x => x.ClientGuid == clientGuid, ct);
    }

    /// <inheritdoc />
    public Task<Meal?> FindLatestInWindowAsync(Guid patientId, DateTime windowStart, DateTime windowEnd, CancellationToken ct = default)
    {
        return _context.Set<Meal>()
            .AsNoTracking()
            .Where(x => x.PatientId == patientId && x.ClientCreatedAt > windowStart && x.ClientCreatedAt <= windowEnd)
            .OrderByDescending(x => x.ClientCreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Meal>> ListByPatientInRangeAsync(
        Guid patientId, DateTime from, DateTime to, int skip, int take, CancellationToken ct = default)
    {
        return await _context.Set<Meal>()
            .AsNoTracking()
            .Include(x => x.Items)
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
        return _context.Set<Meal>()
            .AsNoTracking()
            .CountAsync(x => x.PatientId == patientId && x.ClientCreatedAt >= from && x.ClientCreatedAt <= to, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(Meal meal, CancellationToken ct = default)
    {
        await _context.Set<Meal>().AddAsync(meal, ct).ConfigureAwait(false);
    }
}
