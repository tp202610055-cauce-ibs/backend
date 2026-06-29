using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetClinicalNotes;

/// <summary>
/// Consulta de las notas clínicas del paciente autenticado en un rango de fechas.
/// </summary>
/// <param name="From">Inicio del rango (UTC).</param>
/// <param name="To">Fin del rango (UTC).</param>
public sealed record GetClinicalNotesQuery(DateTime From, DateTime To) : IRequest<IReadOnlyList<ClinicalNoteSummary>>;
