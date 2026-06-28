using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Ingrediente de un alimento personalizado: vincula una entrada del catálogo con la
/// proporción que aporta a la preparación. Es entidad interna del agregado
/// <see cref="CustomFood"/> y no raíz de agregado; se crea y modifica únicamente a
/// través de su raíz.
/// </summary>
public sealed class CustomFoodIngredient : Entity
{
    /// <summary>
    /// Identificador del alimento personalizado al que pertenece.
    /// </summary>
    public Guid CustomFoodId { get; private set; }

    /// <summary>
    /// Identificador del alimento del catálogo que actúa como ingrediente.
    /// </summary>
    public Guid FoodId { get; private set; }

    /// <summary>
    /// Proporción del ingrediente en la preparación, en gramos (mayor que cero).
    /// </summary>
    public decimal ProportionGrams { get; private set; }

    private CustomFoodIngredient()
    {
    }

    private CustomFoodIngredient(Guid id, Guid customFoodId, Guid foodId, decimal proportionGrams)
        : base(id)
    {
        CustomFoodId = customFoodId;
        FoodId = foodId;
        ProportionGrams = proportionGrams;
    }

    /// <summary>
    /// Crea un ingrediente. Lo invoca la raíz de agregado <see cref="CustomFood"/>.
    /// </summary>
    /// <param name="id">Identificador del ingrediente.</param>
    /// <param name="customFoodId">Identificador del alimento personalizado.</param>
    /// <param name="foodId">Identificador del alimento del catálogo.</param>
    /// <param name="proportionGrams">Proporción en gramos (mayor que cero).</param>
    /// <returns>El nuevo ingrediente.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si la proporción no es mayor que cero.</exception>
    internal static CustomFoodIngredient Create(Guid id, Guid customFoodId, Guid foodId, decimal proportionGrams)
    {
        if (proportionGrams <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(proportionGrams), "La proporción del ingrediente debe ser mayor que cero.");
        }

        return new CustomFoodIngredient(id, customFoodId, foodId, proportionGrams);
    }
}
