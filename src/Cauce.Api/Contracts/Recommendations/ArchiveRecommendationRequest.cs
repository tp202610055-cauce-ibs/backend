using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Api.Contracts.Recommendations;

/// <summary>
/// Cuerpo de la petición de archivado de una recomendación (US30 CA01).
/// </summary>
/// <param name="Reason">Motivo del archivado.</param>
public sealed record ArchiveRecommendationRequest(ArchiveReason Reason);
