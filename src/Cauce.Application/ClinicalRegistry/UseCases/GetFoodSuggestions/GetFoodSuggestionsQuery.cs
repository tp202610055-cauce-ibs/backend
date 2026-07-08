using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetFoodSuggestions;

/// <summary>
/// Consulta de las sugerencias de alimentos del paciente autenticado (US09 CA03): alimentos
/// frecuentes, recientes y una selección rotativa del catálogo.
/// </summary>
public sealed record GetFoodSuggestionsQuery : IRequest<FoodSuggestionsResult>;
