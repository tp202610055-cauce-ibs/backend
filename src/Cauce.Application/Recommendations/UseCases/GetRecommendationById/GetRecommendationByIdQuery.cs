using Cauce.Application.Recommendations.Dtos;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.GetRecommendationById;

/// <summary>
/// Consulta el detalle de una recomendación. El acceso lo resuelve el handler según el rol del
/// usuario autenticado: el paciente debe ser el propietario; el nutricionista, estar asignado.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación.</param>
public sealed record GetRecommendationByIdQuery(Guid RecommendationId) : IRequest<RecommendationDetailDto>;
