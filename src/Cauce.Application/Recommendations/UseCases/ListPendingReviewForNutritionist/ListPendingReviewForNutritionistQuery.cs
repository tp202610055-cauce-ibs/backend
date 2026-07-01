using Cauce.Application.Common.Models;
using Cauce.Application.Recommendations.Dtos;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.ListPendingReviewForNutritionist;

/// <summary>
/// Consulta paginada de las recomendaciones en revisión pendiente de los pacientes asignados al
/// nutricionista autenticado.
/// </summary>
/// <param name="Page">Número de página (base 1).</param>
/// <param name="PageSize">Tamaño de página.</param>
public sealed record ListPendingReviewForNutritionistQuery(
    int Page,
    int PageSize) : IRequest<PagedResult<RecommendationSummaryDto>>;
