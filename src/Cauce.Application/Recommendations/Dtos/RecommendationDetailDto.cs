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
/// <param name="ReviewedByNutritionistName">Nombre completo del nutricionista revisor, o <see langword="null"/> (bloque 2, US15 CA02).</param>
/// <param name="Steps">Pasos accionables de la recomendación; vacío si no tiene (bloque 3).</param>
/// <param name="SupportingData">Datos de respaldo clínico de la ventana de análisis (bloque 4).</param>
/// <param name="Title">
/// Título de la recomendación, o <see langword="null"/>. Lo tienen las recomendaciones manuales y las que el
/// nutricionista modificó; las del motor no traen uno propio (acta A69).
/// </param>
/// <param name="Description">Descripción de la recomendación, o <see langword="null"/> (acta A69).</param>
/// <param name="Source">Origen: generada por el motor o creada manualmente por el nutricionista.</param>
/// <param name="IsActive">
/// Indica si la recomendación sigue vigente. Archivar la marca inactiva sin cambiar su estado (acta A22).
/// </param>
/// <param name="ArchivedAt">Momento del archivado, en UTC, o <see langword="null"/> si sigue activa.</param>
/// <param name="ArchiveReason">Motivo del archivado, o <see langword="null"/> si sigue activa.</param>
/// <param name="ValidUntil">
/// Fecha hasta la que rige, en UTC, o <see langword="null"/> si no caduca. Solo la fijan las manuales.
/// </param>
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
    RecommendationFeedbackDto? Feedback,
    string? ReviewedByNutritionistName,
    IReadOnlyList<string> Steps,
    RecommendationSupportingDataDto SupportingData,
    string? Title,
    string? Description,
    RecommendationSource Source,
    bool IsActive,
    DateTime? ArchivedAt,
    ArchiveReason? ArchiveReason,
    DateTime? ValidUntil);
