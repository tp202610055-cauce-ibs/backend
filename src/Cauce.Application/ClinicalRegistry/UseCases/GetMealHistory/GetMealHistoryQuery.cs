using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.Common.Models;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetMealHistory;

/// <summary>
/// Consulta paginada del historial de comidas del paciente autenticado en un rango de
/// fechas.
/// </summary>
/// <param name="From">Inicio del rango (UTC).</param>
/// <param name="To">Fin del rango (UTC).</param>
/// <param name="Page">Número de página (base 1).</param>
/// <param name="PageSize">Tamaño de página.</param>
public sealed record GetMealHistoryQuery(
    DateTime From,
    DateTime To,
    int Page = 1,
    int PageSize = 50) : IRequest<PagedResult<MealHistoryItem>>;
