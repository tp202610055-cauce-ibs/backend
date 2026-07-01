using Cauce.Domain.Common.Exceptions;
using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando se intenta una transición de estado no permitida por la máquina de
/// estados de la recomendación.
/// </summary>
public sealed class InvalidRecommendationStateTransitionException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con los estados de origen y destino de la transición inválida.
    /// </summary>
    /// <param name="from">Estado de origen.</param>
    /// <param name="to">Estado de destino solicitado.</param>
    public InvalidRecommendationStateTransitionException(RecommendationStatus from, RecommendationStatus to)
        : base($"Transición de estado inválida de '{from}' a '{to}'.")
    {
        From = from;
        To = to;
    }

    /// <summary>
    /// Estado de origen de la transición rechazada.
    /// </summary>
    public RecommendationStatus From { get; }

    /// <summary>
    /// Estado de destino de la transición rechazada.
    /// </summary>
    public RecommendationStatus To { get; }
}
