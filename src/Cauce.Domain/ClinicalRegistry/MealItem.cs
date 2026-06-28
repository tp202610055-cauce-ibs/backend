using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Ítem de una comida: referencia un alimento del catálogo o un alimento personalizado
/// (exactamente uno) junto con la cantidad consumida. Es entidad interna del agregado
/// <see cref="Meal"/> y no raíz de agregado.
/// </summary>
public sealed class MealItem : Entity
{
    /// <summary>
    /// Identificador de la comida a la que pertenece.
    /// </summary>
    public Guid MealId { get; private set; }

    /// <summary>
    /// Identificador del alimento del catálogo, o <see langword="null"/> si el ítem
    /// referencia un alimento personalizado.
    /// </summary>
    public Guid? FoodId { get; private set; }

    /// <summary>
    /// Identificador del alimento personalizado, o <see langword="null"/> si el ítem
    /// referencia un alimento del catálogo.
    /// </summary>
    public Guid? CustomFoodId { get; private set; }

    /// <summary>
    /// Cantidad consumida (mayor que cero), expresada en <see cref="Unit"/>.
    /// </summary>
    public decimal Quantity { get; private set; }

    /// <summary>
    /// Unidad de medida de la cantidad.
    /// </summary>
    public MeasurementUnit Unit { get; private set; }

    private MealItem()
    {
    }

    private MealItem(Guid id, Guid mealId, Guid? foodId, Guid? customFoodId, decimal quantity, MeasurementUnit unit)
        : base(id)
    {
        MealId = mealId;
        FoodId = foodId;
        CustomFoodId = customFoodId;
        Quantity = quantity;
        Unit = unit;
    }

    /// <summary>
    /// Crea un ítem de comida. Lo invoca la raíz de agregado <see cref="Meal"/>, que ya
    /// validó la regla de referencia exclusiva y la cantidad.
    /// </summary>
    /// <param name="id">Identificador del ítem.</param>
    /// <param name="mealId">Identificador de la comida.</param>
    /// <param name="foodId">Identificador del alimento del catálogo, o <see langword="null"/>.</param>
    /// <param name="customFoodId">Identificador del alimento personalizado, o <see langword="null"/>.</param>
    /// <param name="quantity">Cantidad consumida.</param>
    /// <param name="unit">Unidad de medida.</param>
    /// <returns>El nuevo ítem de comida.</returns>
    internal static MealItem Create(Guid id, Guid mealId, Guid? foodId, Guid? customFoodId, decimal quantity, MeasurementUnit unit)
    {
        return new MealItem(id, mealId, foodId, customFoodId, quantity, unit);
    }

    /// <summary>
    /// Indica si el ítem referencia un alimento del catálogo (en oposición a uno personalizado).
    /// </summary>
    /// <returns><see langword="true"/> si <see cref="FoodId"/> tiene valor.</returns>
    public bool ReferencesCatalogFood()
    {
        return FoodId.HasValue;
    }
}
