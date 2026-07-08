using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando se intenta archivar una recomendación que no está activa o que no se encuentra en un
/// estado terminal aprobado (<c>Approved</c>, <c>ModifiedApproved</c> o <c>ManualApproved</c>).
/// </summary>
public sealed class RecommendationNotArchivableException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el identificador de la recomendación.
    /// </summary>
    /// <param name="recommendationId">Identificador de la recomendación.</param>
    public RecommendationNotArchivableException(Guid recommendationId)
        : base($"La recomendación '{recommendationId}' no puede archivarse en su estado actual.")
    {
        RecommendationId = recommendationId;
    }

    /// <summary>
    /// Identificador de la recomendación.
    /// </summary>
    public Guid RecommendationId { get; }
}
