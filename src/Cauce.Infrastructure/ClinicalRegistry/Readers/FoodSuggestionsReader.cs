using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.ClinicalRegistry.Readers;

/// <summary>
/// Implementación de <see cref="IFoodSuggestionsReader"/> (US09 CA03). Materializa los ítems de comida
/// acotados por ventana y agrupa en memoria para evitar traducciones LINQ frágiles, siguiendo el mismo
/// criterio que el lector de reportes. Solo considera alimentos del catálogo (no personalizados).
/// </summary>
public sealed class FoodSuggestionsReader : IFoodSuggestionsReader
{
    private const int MaxSuggestions = 10;
    private const int FrequentWindowDays = 30;
    private const int RecentWindowHours = 24;

    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el lector con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public FoodSuggestionsReader(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<FoodSuggestionsResult> GetAsync(
        Guid patientId,
        DateTime utcNow,
        int catalogSeed,
        CancellationToken ct = default)
    {
        var frequentWindowStart = utcNow.AddDays(-FrequentWindowDays);
        var recentWindowStart = utcNow.AddHours(-RecentWindowHours);

        var items = await (from meal in _context.Meals.AsNoTracking()
                           where meal.PatientId == patientId && meal.ConsumedAt >= frequentWindowStart
                           join item in _context.MealItems.AsNoTracking() on meal.Id equals item.MealId
                           where item.FoodId != null
                           select new { FoodId = item.FoodId!.Value, meal.ConsumedAt })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var frequentIds = items
            .GroupBy(item => item.FoodId)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .Take(MaxSuggestions)
            .ToList();

        var recentIds = items
            .Where(item => item.ConsumedAt >= recentWindowStart)
            .GroupBy(item => item.FoodId)
            .OrderByDescending(group => group.Max(item => item.ConsumedAt))
            .Select(group => group.Key)
            .Take(MaxSuggestions)
            .ToList();

        var frequent = await LoadOrderedAsync(frequentIds, ct).ConfigureAwait(false);
        var recent = await LoadOrderedAsync(recentIds, ct).ConfigureAwait(false);
        var catalog = await LoadCatalogSuggestionsAsync(catalogSeed, ct).ConfigureAwait(false);

        return new FoodSuggestionsResult(frequent, recent, catalog);
    }

    /// <summary>
    /// Carga los alimentos activos indicados y los devuelve preservando el orden de la lista de
    /// identificadores. Descarta los que ya no estén activos.
    /// </summary>
    private async Task<IReadOnlyList<FoodItemSummary>> LoadOrderedAsync(
        IReadOnlyList<Guid> foodIds,
        CancellationToken ct)
    {
        if (foodIds.Count == 0)
        {
            return [];
        }

        var foods = await _context.FoodItems
            .AsNoTracking()
            .Where(food => foodIds.Contains(food.Id) && food.IsActive)
            .ToDictionaryAsync(food => food.Id, ct)
            .ConfigureAwait(false);

        return foodIds
            .Where(foods.ContainsKey)
            .Select(id => ClinicalRegistryMappings.ToSummary(foods[id]))
            .ToList();
    }

    /// <summary>
    /// Selecciona hasta 10 alimentos del catálogo de forma determinista según la semilla semanal: se
    /// ordena por nombre y se toma una ventana desplazada por la semilla, que rota cada semana.
    /// </summary>
    private async Task<IReadOnlyList<FoodItemSummary>> LoadCatalogSuggestionsAsync(int catalogSeed, CancellationToken ct)
    {
        var total = await _context.FoodItems
            .AsNoTracking()
            .CountAsync(food => food.IsActive, ct)
            .ConfigureAwait(false);
        if (total == 0)
        {
            return [];
        }

        var offsetSpan = Math.Max(1, total - MaxSuggestions);
        var offset = catalogSeed % offsetSpan;

        var foods = await _context.FoodItems
            .AsNoTracking()
            .Where(food => food.IsActive)
            .OrderBy(food => food.Name)
            .ThenBy(food => food.Id)
            .Skip(offset)
            .Take(MaxSuggestions)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return foods.Select(ClinicalRegistryMappings.ToSummary).ToList();
    }
}
