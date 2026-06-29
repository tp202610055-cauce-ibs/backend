using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando se intenta eliminar un alimento personalizado referenciado por
/// ítems de comida.
/// </summary>
public sealed class CustomFoodInUseException : DomainException
{
    /// <summary>
    /// Identificador del alimento personalizado en uso.
    /// </summary>
    public Guid CustomFoodId { get; }

    /// <summary>
    /// Inicializa la excepción con el identificador del alimento personalizado.
    /// </summary>
    /// <param name="customFoodId">Identificador del alimento personalizado.</param>
    public CustomFoodInUseException(Guid customFoodId)
        : base($"El alimento personalizado {customFoodId} está referenciado por comidas y no puede eliminarse.")
    {
        CustomFoodId = customFoodId;
    }
}
