using Cauce.Application.Notifications.Dtos;
using MediatR;

namespace Cauce.Application.Notifications.UseCases.ListNutritionistAlerts;

/// <summary>
/// Consulta paginada de la bandeja de avisos del nutricionista autenticado.
/// </summary>
/// <param name="Page">Número de página, desde 1.</param>
/// <param name="PageSize">Tamaño de página, de 1 a 100.</param>
public sealed record ListNutritionistAlertsQuery(int Page, int PageSize) : IRequest<NutritionistAlertListResult>;
