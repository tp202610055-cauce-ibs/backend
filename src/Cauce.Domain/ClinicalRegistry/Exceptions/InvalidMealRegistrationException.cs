using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando el registro de una comida viola una invariante de dominio (número
/// de ítems fuera de rango, marca temporal futura, o un ítem sin referencia de
/// alimento válida).
/// </summary>
public sealed class InvalidMealRegistrationException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje descriptivo de la violación.
    /// </summary>
    /// <param name="message">Descripción de la invariante violada (sin datos clínicos).</param>
    public InvalidMealRegistrationException(string message)
        : base(message)
    {
    }
}
