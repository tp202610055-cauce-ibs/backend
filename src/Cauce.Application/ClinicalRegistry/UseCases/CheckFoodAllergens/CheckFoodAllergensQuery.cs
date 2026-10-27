using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.CheckFoodAllergens;

/// <summary>
/// Consulta del paciente autenticado: si un alimento del catálogo choca con sus alergias declaradas.
/// </summary>
/// <param name="FoodId">Identificador del alimento del catálogo.</param>
public sealed record CheckFoodAllergensQuery(Guid FoodId) : IRequest<FoodAllergenCheckResult>;
