using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando se intenta operar sobre una recomendación que ya expiró.
/// </summary>
public sealed class RecommendationExpiredException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el identificador de la recomendación expirada.
    /// </summary>
    /// <param name="recommendationId">Identificador de la recomendación.</param>
    public RecommendationExpiredException(Guid recommendationId)
        : base($"La recomendación '{recommendationId}' ha expirado y no puede modificarse.")
    {
        RecommendationId = recommendationId;
    }

    /// <summary>
    /// Identificador de la recomendación expirada.
    /// </summary>
    public Guid RecommendationId { get; }
}
