using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetFoodItemDetail;

/// <summary>
/// Consulta del detalle de un alimento del catálogo. Accesible para cualquier usuario
/// autenticado.
/// </summary>
/// <param name="FoodId">Identificador del alimento.</param>
public sealed record GetFoodItemDetailQuery(Guid FoodId) : IRequest<FoodItemDetail>;
