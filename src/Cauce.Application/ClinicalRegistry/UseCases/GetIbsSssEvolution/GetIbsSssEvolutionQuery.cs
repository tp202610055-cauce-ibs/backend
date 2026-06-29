using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetIbsSssEvolution;

/// <summary>
/// Consulta de la evolución IBS-SSS del paciente autenticado: todas sus evaluaciones
/// ordenadas por número de ciclo ascendente, con la diferencia de puntaje respecto de
/// la línea base.
/// </summary>
public sealed record GetIbsSssEvolutionQuery : IRequest<IReadOnlyList<IbsSssEvolutionEntry>>;
