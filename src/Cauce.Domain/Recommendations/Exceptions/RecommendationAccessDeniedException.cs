using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando un usuario intenta acceder a una recomendación sobre la que no tiene
/// autorización (un paciente que no es el propietario, o un nutricionista no asignado al
/// paciente). No revela información del recurso para evitar filtrar su existencia.
/// </summary>
public sealed class RecommendationAccessDeniedException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con su mensaje estándar.
    /// </summary>
    public RecommendationAccessDeniedException()
        : base("No tiene autorización para acceder a esta recomendación.")
    {
    }
}
