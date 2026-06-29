using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando se referencia un alimento del catálogo que no existe o no está activo.
/// </summary>
public sealed class FoodItemNotFoundException : DomainException
{
    /// <summary>
    /// Identificador del alimento no encontrado.
    /// </summary>
    public Guid FoodId { get; }

    /// <summary>
    /// Inicializa la excepción con el identificador del alimento.
    /// </summary>
    /// <param name="foodId">Identificador del alimento del catálogo.</param>
    public FoodItemNotFoundException(Guid foodId)
        : base($"El alimento {foodId} no existe o no está activo.")
    {
        FoodId = foodId;
    }
}
