using Cauce.Application.Recommendations.Contracts;
using Cauce.Application.Recommendations.Dtos;
using Cauce.Domain.Recommendations;

namespace Cauce.Application.Recommendations.Mapping;

/// <summary>
/// Mapeos entre las entidades del módulo de recomendaciones y sus DTOs de respuesta.
/// </summary>
public static class RecommendationsMappings
{
    /// <summary>
    /// Proyecta una recomendación a su resumen para listados.
    /// </summary>
    /// <param name="recommendation">Recomendación a proyectar.</param>
    /// <returns>El resumen de la recomendación.</returns>
    public static RecommendationSummaryDto ToSummary(Recommendation recommendation)
    {
        return new RecommendationSummaryDto(
            recommendation.Id,
            recommendation.Status,
            recommendation.ConfidenceScore.Value,
            recommendation.Items.Count,
            recommendation.GeneratedAt,
            recommendation.ExpiresAt);
    }

    /// <summary>
    /// Proyecta una recomendación a su detalle completo, enriqueciendo sus ítems con los nombres
    /// legibles de los alimentos.
    /// </summary>
    /// <param name="recommendation">Recomendación a proyectar.</param>
    /// <param name="modelVersionName">Nombre de la versión de modelo usada.</param>
    /// <param name="foodNames">Datos legibles de los alimentos involucrados, por identificador.</param>
    /// <returns>El detalle de la recomendación.</returns>
    public static RecommendationDetailDto ToDetail(
        Recommendation recommendation,
        string modelVersionName,
        IReadOnlyDictionary<Guid, FoodNameInfo> foodNames)
    {
        var items = recommendation.Items
            .Select(item => ToItemDto(item, foodNames))
            .ToList();

        return new RecommendationDetailDto(
            recommendation.Id,
            recommendation.PatientId,
            modelVersionName,
            recommendation.Status,
            recommendation.ConfidenceScore.Value,
            recommendation.AutoApproved,
            recommendation.ReviewedByNutritionistId,
            recommendation.NutritionistNote,
            recommendation.AiExplanation,
            recommendation.ExplanationSource,
            recommendation.GeneratedAt,
            recommendation.ReviewedAt,
            recommendation.DeliveredAt,
            recommendation.ExpiresAt,
            items,
            recommendation.Feedback is null ? null : ToFeedbackDto(recommendation.Feedback));
    }

    /// <summary>
    /// Proyecta un ítem de recomendación a su DTO, resolviendo los nombres de su alimento y su
    /// sustituto.
    /// </summary>
    /// <param name="item">Ítem a proyectar.</param>
    /// <param name="foodNames">Datos legibles de los alimentos, por identificador.</param>
    /// <returns>El DTO del ítem.</returns>
    public static RecommendationItemDto ToItemDto(
        RecommendationItem item,
        IReadOnlyDictionary<Guid, FoodNameInfo> foodNames)
    {
        var food = foodNames.GetValueOrDefault(item.FoodId);
        var substitute = item.SubstituteFoodId.HasValue
            ? foodNames.GetValueOrDefault(item.SubstituteFoodId.Value)
            : null;

        return new RecommendationItemDto(
            item.Id,
            item.FoodId,
            food?.Name ?? string.Empty,
            food?.Category ?? string.Empty,
            item.ActionType,
            item.SubstituteFoodId,
            substitute?.Name,
            item.Reasoning);
    }

    /// <summary>
    /// Proyecta la retroalimentación de una recomendación a su DTO.
    /// </summary>
    /// <param name="feedback">Retroalimentación a proyectar.</param>
    /// <returns>El DTO de la retroalimentación.</returns>
    public static RecommendationFeedbackDto ToFeedbackDto(RecommendationFeedback feedback)
    {
        return new RecommendationFeedbackDto(
            feedback.Id,
            feedback.WasApplied,
            feedback.Outcome,
            feedback.Comment,
            feedback.SubmittedAt);
    }
}
