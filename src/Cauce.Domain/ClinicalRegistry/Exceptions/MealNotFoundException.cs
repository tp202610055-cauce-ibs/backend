using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando se referencia una comida que no existe.
/// </summary>
public sealed class MealNotFoundException : DomainException
{
    /// <summary>
    /// Identificador de la comida no encontrada.
    /// </summary>
    public Guid MealId { get; }

    /// <summary>
    /// Inicializa la excepción con el identificador de la comida.
    /// </summary>
    /// <param name="mealId">Identificador de la comida.</param>
    public MealNotFoundException(Guid mealId)
        : base($"La comida {mealId} no existe.")
    {
        MealId = mealId;
    }
}
