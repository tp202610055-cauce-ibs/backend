using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetGlossary;

/// <summary>
/// Consulta que devuelve todo el glosario clínico ordenado alfabéticamente, con la definición
/// apropiada al rol del solicitante (US27).
/// </summary>
public sealed record GetGlossaryQuery : IRequest<GlossaryResult>;
