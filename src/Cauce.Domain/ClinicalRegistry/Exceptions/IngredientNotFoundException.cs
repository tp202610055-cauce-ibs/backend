using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando se intenta quitar de un alimento personalizado un ingrediente que
/// no está presente.
/// </summary>
public sealed class IngredientNotFoundException : DomainException
{
    /// <summary>
    /// Identificador del alimento del catálogo no encontrado entre los ingredientes.
    /// </summary>
    public Guid FoodId { get; }

    /// <summary>
    /// Inicializa la excepción con el identificador del alimento.
    /// </summary>
    /// <param name="foodId">Identificador del alimento del catálogo.</param>
    public IngredientNotFoundException(Guid foodId)
        : base($"El alimento {foodId} no es ingrediente de este alimento personalizado.")
    {
        FoodId = foodId;
    }
}
