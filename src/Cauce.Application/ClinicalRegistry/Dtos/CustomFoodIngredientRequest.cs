namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Ingrediente de un alimento personalizado recibido en un comando de creación o
/// actualización.
/// </summary>
/// <param name="FoodId">Identificador del alimento del catálogo.</param>
/// <param name="ProportionGrams">Proporción en gramos (mayor que cero).</param>
public sealed record CustomFoodIngredientRequest(Guid FoodId, decimal ProportionGrams);
