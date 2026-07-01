using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.UseCases.GenerateRecommendation;

/// <summary>
/// Resultado de la generación de una recomendación.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación generada.</param>
/// <param name="Status">Estado resultante (revisión pendiente o aprobada).</param>
/// <param name="RequiresReview">Indica si la recomendación quedó pendiente de revisión humana.</param>
/// <param name="GeneratedAt">Momento de generación, en UTC.</param>
/// <param name="ExpiresAt">Momento de expiración, en UTC.</param>
public sealed record GenerateRecommendationResult(
    Guid RecommendationId,
    RecommendationStatus Status,
    bool RequiresReview,
    DateTime GeneratedAt,
    DateTime? ExpiresAt);
