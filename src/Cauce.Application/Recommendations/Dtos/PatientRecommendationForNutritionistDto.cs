using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.Dtos;

/// <summary>
/// Fila de la lista de recomendaciones de un paciente que ve su nutricionista: incluye todas, vigentes
/// y archivadas. El contenido completo se lee en el detalle de la recomendación.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación.</param>
/// <param name="Status">Estado actual.</param>
/// <param name="Source">Origen: generada por el motor o creada por el nutricionista.</param>
/// <param name="Title">Título escrito por el nutricionista, o <see langword="null"/> en las generadas.</param>
/// <param name="ItemsCount">Cantidad de alimentos que incluye.</param>
/// <param name="GeneratedAt">Momento de generación o creación, en UTC.</param>
/// <param name="ReviewedAt">Momento de la decisión del nutricionista, en UTC, o <see langword="null"/>.</param>
/// <param name="ExpiresAt">Fin de la ventana de revisión, en UTC, o <see langword="null"/>.</param>
/// <param name="ValidUntil">Fin de la vigencia de una aprobada, en UTC, o <see langword="null"/> si no vence.</param>
/// <param name="IsActive">Indica si sigue vigente; <see langword="false"/> si fue archivada.</param>
/// <param name="ArchivedAt">Momento del archivado, en UTC, o <see langword="null"/> si sigue activa.</param>
/// <param name="ArchiveReason">Motivo del archivado, o <see langword="null"/> si sigue activa.</param>
public sealed record PatientRecommendationForNutritionistDto(
    Guid RecommendationId,
    RecommendationStatus Status,
    RecommendationSource Source,
    string? Title,
    int ItemsCount,
    DateTime GeneratedAt,
    DateTime? ReviewedAt,
    DateTime? ExpiresAt,
    DateTime? ValidUntil,
    bool IsActive,
    DateTime? ArchivedAt,
    ArchiveReason? ArchiveReason);
