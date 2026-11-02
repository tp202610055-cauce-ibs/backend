namespace Cauce.Application.Recommendations.Dtos;

/// <summary>
/// Indicador para el paciente de que tiene una recomendación en revisión por su nutricionista. No
/// incluye contenido, fechas ni conteos.
/// </summary>
/// <param name="HasRecommendationInReview">Indica si hay al menos una recomendación activa en revisión y sin vencer.</param>
public sealed record RecommendationReviewStatusDto(bool HasRecommendationInReview);
