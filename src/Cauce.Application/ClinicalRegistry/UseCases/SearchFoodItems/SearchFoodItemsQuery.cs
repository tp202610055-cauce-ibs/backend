using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.SearchFoodItems;

/// <summary>
/// Búsqueda del catálogo de alimentos por coincidencia de nombre, limitada a 20
/// resultados. Accesible para cualquier usuario autenticado.
/// </summary>
/// <param name="Query">Texto a buscar en el nombre del alimento.</param>
public sealed record SearchFoodItemsQuery(string Query) : IRequest<IReadOnlyList<FoodItemSummary>>;
