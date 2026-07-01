using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Recommendations.Readers;

/// <summary>
/// Implementación de <see cref="IFoodCatalogReader"/> que resuelve nombres y categorías de
/// alimentos a partir de sus identificadores.
/// </summary>
public sealed class FoodCatalogReader : IFoodCatalogReader
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el lector con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public FoodCatalogReader(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, FoodNameInfo>> GetFoodNamesAsync(
        IReadOnlyCollection<Guid> foodIds,
        CancellationToken ct = default)
    {
        if (foodIds.Count == 0)
        {
            return new Dictionary<Guid, FoodNameInfo>();
        }

        var ids = foodIds.Distinct().ToList();

        return await _context.Set<FoodItem>()
            .AsNoTracking()
            .Where(food => ids.Contains(food.Id))
            .ToDictionaryAsync(food => food.Id, food => new FoodNameInfo(food.Name, food.Category), ct)
            .ConfigureAwait(false);
    }
}
