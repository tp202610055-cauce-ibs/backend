using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando se intenta agregar a un alimento personalizado un ingrediente cuyo
/// alimento del catálogo ya está presente.
/// </summary>
public sealed class DuplicateIngredientException : DomainException
{
    /// <summary>
    /// Identificador del alimento del catálogo duplicado.
    /// </summary>
    public Guid FoodId { get; }

    /// <summary>
    /// Inicializa la excepción con el identificador del alimento.
    /// </summary>
    /// <param name="foodId">Identificador del alimento del catálogo.</param>
    public DuplicateIngredientException(Guid foodId)
        : base($"El alimento {foodId} ya es ingrediente de este alimento personalizado.")
    {
        FoodId = foodId;
    }
}
