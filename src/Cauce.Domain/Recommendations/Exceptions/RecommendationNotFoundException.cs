using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando no existe una recomendación con el identificador solicitado.
/// </summary>
public sealed class RecommendationNotFoundException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el identificador de la recomendación no encontrada.
    /// </summary>
    /// <param name="recommendationId">Identificador de la recomendación.</param>
    public RecommendationNotFoundException(Guid recommendationId)
        : base($"No existe la recomendación '{recommendationId}'.")
    {
        RecommendationId = recommendationId;
    }

    /// <summary>
    /// Identificador de la recomendación no encontrada.
    /// </summary>
    public Guid RecommendationId { get; }
}
