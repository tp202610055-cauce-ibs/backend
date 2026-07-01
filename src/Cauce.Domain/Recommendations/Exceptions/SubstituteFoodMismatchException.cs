using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando un ítem de recomendación viola las reglas de su alimento sustituto
/// (por ejemplo, un sustituto en una acción que no es de sustitución, o un sustituto igual
/// al alimento original).
/// </summary>
public sealed class SubstituteFoodMismatchException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con la razón específica de la violación.
    /// </summary>
    /// <param name="reason">Descripción de la regla violada.</param>
    public SubstituteFoodMismatchException(string reason)
        : base($"Alimento sustituto inválido: {reason}")
    {
    }
}
