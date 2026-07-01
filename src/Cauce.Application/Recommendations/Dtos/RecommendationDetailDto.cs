using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.Dtos;

/// <summary>
/// Detalle completo de una recomendación: sus metadatos, su explicación, sus ítems y, si
/// existe, su retroalimentación.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación.</param>
/// <param name="PatientId">Identificador del paciente destinatario.</param>
/// <param name="ModelVersionName">Nombre de la versión de modelo usada.</param>
/// <param name="Status">Estado actual.</param>
/// <param name="ConfidenceScore">Puntaje de confianza.</param>
/// <param name="AutoApproved">Indica si fue auto-aprobada.</param>
/// <param name="ReviewedByNutritionistId">Identificador del nutricionista revisor, o <see langword="null"/>.</param>
/// <param name="NutritionistNote">Nota o motivo del nutricionista, o <see langword="null"/>.</param>
/// <param name="AiExplanation">Explicación en lenguaje natural, o <see langword="null"/>.</param>
/// <param name="ExplanationSource">Origen de la explicación.</param>
/// <param name="GeneratedAt">Momento de generación, en UTC.</param>
/// <param name="ReviewedAt">Momento de revisión, en UTC, o <see langword="null"/>.</param>
/// <param name="DeliveredAt">Momento de entrega, en UTC, o <see langword="null"/>.</param>
/// <param name="ExpiresAt">Momento de expiración, en UTC, o <see langword="null"/>.</param>
/// <param name="Items">Ítems de la recomendación.</param>
/// <param name="Feedback">Retroalimentación del paciente, o <see langword="null"/>.</param>
public sealed record RecommendationDetailDto(
    Guid RecommendationId,
    Guid PatientId,
    string ModelVersionName,
    RecommendationStatus Status,
    decimal ConfidenceScore,
    bool AutoApproved,
    Guid? ReviewedByNutritionistId,
    string? NutritionistNote,
    string? AiExplanation,
    ExplanationSource ExplanationSource,
    DateTime GeneratedAt,
    DateTime? ReviewedAt,
    DateTime? DeliveredAt,
    DateTime? ExpiresAt,
    IReadOnlyList<RecommendationItemDto> Items,
    RecommendationFeedbackDto? Feedback);
