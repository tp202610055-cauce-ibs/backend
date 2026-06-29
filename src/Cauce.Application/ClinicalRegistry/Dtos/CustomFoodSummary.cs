namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Resumen de un ingrediente de un alimento personalizado.
/// </summary>
/// <param name="FoodId">Identificador del alimento del catálogo.</param>
/// <param name="ProportionGrams">Proporción en gramos.</param>
public sealed record CustomFoodIngredientSummary(Guid FoodId, decimal ProportionGrams);

/// <summary>
/// Resumen de un alimento personalizado del paciente, con sus ingredientes.
/// </summary>
/// <param name="CustomFoodId">Identificador del alimento personalizado.</param>
/// <param name="Name">Nombre del alimento personalizado.</param>
/// <param name="PortionSizeGrams">Tamaño de porción en gramos.</param>
/// <param name="CreatedAt">Momento de creación, en UTC.</param>
/// <param name="Ingredients">Ingredientes del alimento personalizado.</param>
public sealed record CustomFoodSummary(
    Guid CustomFoodId,
    string Name,
    decimal PortionSizeGrams,
    DateTime CreatedAt,
    IReadOnlyList<CustomFoodIngredientSummary> Ingredients);
