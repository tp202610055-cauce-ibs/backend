using Cauce.Application.Recommendations.Dtos;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.GetPatientReviewStatus;

/// <summary>
/// Consulta si el paciente autenticado tiene una recomendación en revisión.
/// </summary>
public sealed record GetPatientReviewStatusQuery : IRequest<RecommendationReviewStatusDto>;
