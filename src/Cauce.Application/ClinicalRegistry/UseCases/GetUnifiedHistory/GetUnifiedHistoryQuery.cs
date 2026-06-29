using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetUnifiedHistory;

/// <summary>
/// Consulta del historial unificado del paciente autenticado (comidas, síntomas y notas
/// clínicas) en un rango de fechas, ordenado cronológicamente de forma descendente.
/// </summary>
/// <param name="From">Inicio del rango (UTC).</param>
/// <param name="To">Fin del rango (UTC).</param>
public sealed record GetUnifiedHistoryQuery(DateTime From, DateTime To) : IRequest<IReadOnlyList<HistoryEvent>>;
