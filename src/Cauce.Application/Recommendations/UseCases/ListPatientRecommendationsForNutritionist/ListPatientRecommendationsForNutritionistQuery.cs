using Cauce.Application.Common.Models;
using Cauce.Application.Recommendations.Dtos;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.ListPatientRecommendationsForNutritionist;

/// <summary>
/// Consulta paginada de todas las recomendaciones de un paciente asignado, vigentes y archivadas, para
/// su nutricionista. Requiere asignación activa y queda auditada.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
/// <param name="Page">Número de página (base 1).</param>
/// <param name="PageSize">Tamaño de página.</param>
public sealed record ListPatientRecommendationsForNutritionistQuery(Guid PatientUserId, int Page, int PageSize)
    : IRequest<PagedResult<PatientRecommendationForNutritionistDto>>;
