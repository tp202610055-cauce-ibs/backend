using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IFoodItemRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class FoodItemRepository : IFoodItemRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public FoodItemRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<FoodItem?> FindByIdAsync(Guid foodId, CancellationToken ct = default)
    {
        return _context.Set<FoodItem>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == foodId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FoodItem>> ListActiveAsync(
        int skip, int take, string? categoryFilter, FodmapLevel? fodmapFilter, CancellationToken ct = default)
    {
        return await BuildActiveQuery(categoryFilter, fodmapFilter)
            .OrderBy(x => x.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FoodItem>> SearchByNameAsync(string query, int take, CancellationToken ct = default)
    {
        return await _context.Set<FoodItem>()
            .AsNoTracking()
            .Where(x => x.IsActive && EF.Functions.ILike(x.Name, $"%{query}%"))
            .OrderBy(x => x.Name)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<int> CountActiveAsync(string? categoryFilter, FodmapLevel? fodmapFilter, CancellationToken ct = default)
    {
        return BuildActiveQuery(categoryFilter, fodmapFilter).CountAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
    {
        return _context.Set<FoodItem>().AsNoTracking().AnyAsync(x => x.Name == name, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(FoodItem foodItem, CancellationToken ct = default)
    {
        await _context.Set<FoodItem>().AddAsync(foodItem, ct).ConfigureAwait(false);
    }

    private IQueryable<FoodItem> BuildActiveQuery(string? categoryFilter, FodmapLevel? fodmapFilter)
    {
        var query = _context.Set<FoodItem>().AsNoTracking().Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(categoryFilter))
        {
            query = query.Where(x => x.Category == categoryFilter);
        }

        if (fodmapFilter.HasValue)
        {
            query = query.Where(x => x.FodmapLevel == fodmapFilter.Value);
        }

        return query;
    }
}
