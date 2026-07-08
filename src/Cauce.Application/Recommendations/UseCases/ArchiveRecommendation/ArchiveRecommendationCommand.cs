using Cauce.Domain.Recommendations.Enums;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.ArchiveRecommendation;

/// <summary>
/// Comando para archivar una recomendación (marcarla inactiva) con un motivo (US30 CA01).
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación.</param>
/// <param name="Reason">Motivo del archivado.</param>
public sealed record ArchiveRecommendationCommand(Guid RecommendationId, ArchiveReason Reason) : IRequest<Unit>;
