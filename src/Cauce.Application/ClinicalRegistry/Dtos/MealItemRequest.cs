using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Ítem de comida recibido en un comando de creación. Exactamente uno de
/// <see cref="FoodId"/> o <see cref="CustomFoodId"/> debe tener valor.
/// </summary>
/// <param name="FoodId">Identificador del alimento del catálogo, o <see langword="null"/>.</param>
/// <param name="CustomFoodId">Identificador del alimento personalizado, o <see langword="null"/>.</param>
/// <param name="Quantity">Cantidad consumida (mayor que cero).</param>
/// <param name="Unit">Unidad de medida de la cantidad.</param>
public sealed record MealItemRequest(Guid? FoodId, Guid? CustomFoodId, decimal Quantity, MeasurementUnit Unit);
