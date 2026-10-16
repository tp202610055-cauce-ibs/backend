using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Alimento del catálogo tal como lo ve el generador: se elige por nombre, nunca por id.
/// </summary>
public sealed record CatalogFood(string Name, FodmapLevel Level, bool IsPeruvian);

/// <summary>
/// Ítem planificado de una comida: un alimento del catálogo por nombre o un plato personalizado por índice.
/// </summary>
public sealed record PlannedMealItem(string? FoodName, int? CustomFoodIndex, decimal Quantity, MeasurementUnit Unit);

/// <summary>
/// Comida planificada, con fechas en UTC.
/// </summary>
public sealed record PlannedMeal(
    Guid Id,
    Guid ClientGuid,
    MealTime MealTime,
    DateTime ConsumedAt,
    DateTime ClientCreatedAt,
    IReadOnlyList<PlannedMealItem> Items);

/// <summary>
/// Síntoma planificado, con fechas en UTC y la comida que le asigna la regla de las 4 horas.
/// </summary>
public sealed record PlannedSymptom(
    Guid Id,
    Guid ClientGuid,
    SymptomType Type,
    int Intensity,
    DateTime OccurredAt,
    DateTime ClientCreatedAt,
    Guid? AssociatedMealId);

/// <summary>
/// Ingrediente planificado de un plato personalizado.
/// </summary>
public sealed record PlannedIngredient(string FoodName, decimal Grams);

/// <summary>
/// Plato personalizado planificado del paciente.
/// </summary>
public sealed record PlannedCustomFood(Guid Id, string Name, IReadOnlyList<PlannedIngredient> Ingredients)
{
    /// <summary>
    /// Porción en gramos: la suma de sus ingredientes.
    /// </summary>
    public decimal PortionSizeGrams => Ingredients.Sum(ingredient => ingredient.Grams);
}

/// <summary>
/// Grupo de registros con el mismo <c>clientCreatedAt</c> sobre un corte de página.
/// </summary>
/// <param name="Kind"><c>meals</c> o <c>symptoms</c>.</param>
/// <param name="ClientCreatedAt">Instante compartido, en UTC.</param>
/// <param name="FirstPosition">Primera posición del grupo en el orden del servidor, base 1.</param>
/// <param name="Count">Cantidad de registros del grupo.</param>
public sealed record PlannedTieGroup(string Kind, DateTime ClientCreatedAt, int FirstPosition, int Count);

/// <summary>
/// Historial completo planificado para el paciente de volumen.
/// </summary>
public sealed record VolumeHistoryPlan(
    DateTime SeededAtUtc,
    IReadOnlyList<PlannedCustomFood> CustomFoods,
    IReadOnlyList<PlannedMeal> Meals,
    IReadOnlyList<PlannedSymptom> Symptoms,
    IReadOnlyList<PlannedTieGroup> TieGroups);
