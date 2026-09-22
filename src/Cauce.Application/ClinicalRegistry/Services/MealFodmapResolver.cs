using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.Services;

/// <summary>
/// Resuelve el nivel FODMAP agregado de un conjunto de comidas ya materializadas, en una sola
/// consulta al catálogo.
///
/// <para>Existe porque <c>POST /meals</c> devolvía el nivel agregado y <c>GET /meals</c> lo devolvía
/// siempre en <see langword="null"/>: la misma comida respondía "Moderate" al registrarse y nada al
/// releerse, de modo que el distintivo FODMAP del diario en la app nunca aparecía. La regla de
/// agregación es la misma en los dos caminos, incluido el criterio de excluir los alimentos
/// personalizados, cuyo nivel se derivaría de sus ingredientes y todavía no se calcula.</para>
/// </summary>
public sealed class MealFodmapResolver : IMealFodmapResolver
{
    private readonly IFoodItemRepository _foodItemRepository;
    private readonly IFodmapAggregator _fodmapAggregator;

    /// <summary>
    /// Inicializa el resolutor con sus dependencias.
    /// </summary>
    /// <param name="foodItemRepository">Repositorio del catálogo de alimentos.</param>
    /// <param name="fodmapAggregator">Agregador de carga FODMAP.</param>
    public MealFodmapResolver(IFoodItemRepository foodItemRepository, IFodmapAggregator fodmapAggregator)
    {
        _foodItemRepository = foodItemRepository;
        _fodmapAggregator = fodmapAggregator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, FodmapLevel>> ResolveAsync(
        IReadOnlyCollection<Meal> meals,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(meals);
        if (meals.Count == 0)
        {
            return new Dictionary<Guid, FodmapLevel>();
        }

        var foodIds = meals
            .SelectMany(meal => meal.Items)
            .Where(item => item.FoodId.HasValue)
            .Select(item => item.FoodId!.Value)
            .Distinct()
            .ToList();

        var levels = await _foodItemRepository.GetFodmapLevelsAsync(foodIds, ct).ConfigureAwait(false);

        var result = new Dictionary<Guid, FodmapLevel>(meals.Count);
        foreach (var meal in meals)
        {
            var inputs = meal.Items
                .Where(item => item.FoodId.HasValue && levels.ContainsKey(item.FoodId.Value))
                .Select(item => (levels[item.FoodId!.Value], item.Quantity))
                .ToList();

            result[meal.Id] = _fodmapAggregator.AggregateForMeal(inputs);
        }

        return result;
    }
}
