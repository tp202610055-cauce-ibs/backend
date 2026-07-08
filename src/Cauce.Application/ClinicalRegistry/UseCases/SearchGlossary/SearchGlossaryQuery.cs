using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.SearchGlossary;

/// <summary>
/// Consulta que busca términos del glosario clínico por coincidencia de texto (insensible a mayúsculas
/// y a tildes), con la definición apropiada al rol del solicitante (US27).
/// </summary>
/// <param name="Query">Texto a buscar.</param>
public sealed record SearchGlossaryQuery(string Query) : IRequest<GlossaryResult>;
