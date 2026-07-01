using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.Dtos;

/// <summary>
/// Resumen de una recomendación para listados paginados.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación.</param>
/// <param name="Status">Estado actual.</param>
/// <param name="ConfidenceScore">Puntaje de confianza.</param>
/// <param name="ItemsCount">Cantidad de ítems.</param>
/// <param name="GeneratedAt">Momento de generación, en UTC.</param>
/// <param name="ExpiresAt">Momento de expiración, en UTC, o <see langword="null"/>.</param>
public sealed record RecommendationSummaryDto(
    Guid RecommendationId,
    RecommendationStatus Status,
    decimal ConfidenceScore,
    int ItemsCount,
    DateTime GeneratedAt,
    DateTime? ExpiresAt);
