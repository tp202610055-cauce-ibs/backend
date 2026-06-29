using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.ListCustomFoods;

/// <summary>
/// Consulta de los alimentos personalizados del paciente autenticado.
/// </summary>
public sealed record ListCustomFoodsQuery : IRequest<IReadOnlyList<CustomFoodSummary>>;
