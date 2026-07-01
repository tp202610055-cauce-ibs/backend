using Cauce.Application.Common.Models;
using Cauce.Application.Recommendations.Dtos;
using Cauce.Domain.Recommendations.Enums;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.ListPatientRecommendations;

/// <summary>
/// Consulta paginada de las recomendaciones del paciente autenticado, opcionalmente filtradas
/// por estado.
/// </summary>
/// <param name="FilterStatus">Estado por el que filtrar, o <see langword="null"/> para todos.</param>
/// <param name="Page">Número de página (base 1).</param>
/// <param name="PageSize">Tamaño de página.</param>
public sealed record ListPatientRecommendationsQuery(
    RecommendationStatus? FilterStatus,
    int Page,
    int PageSize) : IRequest<PagedResult<RecommendationSummaryDto>>;
