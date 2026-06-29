using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando se referencia un alimento personalizado que no existe.
/// </summary>
public sealed class CustomFoodNotFoundException : DomainException
{
    /// <summary>
    /// Identificador del alimento personalizado no encontrado.
    /// </summary>
    public Guid CustomFoodId { get; }

    /// <summary>
    /// Inicializa la excepción con el identificador del alimento personalizado.
    /// </summary>
    /// <param name="customFoodId">Identificador del alimento personalizado.</param>
    public CustomFoodNotFoundException(Guid customFoodId)
        : base($"El alimento personalizado {customFoodId} no existe.")
    {
        CustomFoodId = customFoodId;
    }
}
