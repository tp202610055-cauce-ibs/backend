using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Datos de entrada de un ítem de comida al registrar una <see cref="Meal"/>. Exactamente
/// uno de <see cref="FoodId"/> o <see cref="CustomFoodId"/> debe tener valor.
/// </summary>
/// <param name="FoodId">Identificador del alimento del catálogo, o <see langword="null"/>.</param>
/// <param name="CustomFoodId">Identificador del alimento personalizado, o <see langword="null"/>.</param>
/// <param name="Quantity">Cantidad consumida (mayor que cero).</param>
/// <param name="Unit">Unidad de medida de la cantidad.</param>
public sealed record MealItemInput(Guid? FoodId, Guid? CustomFoodId, decimal Quantity, MeasurementUnit Unit);
