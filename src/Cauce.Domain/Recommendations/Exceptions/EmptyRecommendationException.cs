using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando se intenta crear una recomendación sin ítems.
/// </summary>
public sealed class EmptyRecommendationException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con su mensaje estándar.
    /// </summary>
    public EmptyRecommendationException()
        : base("Una recomendación debe contener al menos un ítem.")
    {
    }
}
