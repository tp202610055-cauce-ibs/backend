using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.SearchFoodItems;

/// <summary>
/// Handler de la búsqueda del catálogo de alimentos por nombre.
/// </summary>
public sealed class SearchFoodItemsQueryHandler : IRequestHandler<SearchFoodItemsQuery, IReadOnlyList<FoodItemSummary>>
{
    private const int MaxResults = 20;

    private readonly IFoodItemRepository _foodItemRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    /// <param name="foodItemRepository">Repositorio del catálogo de alimentos.</param>
    public SearchFoodItemsQueryHandler(IFoodItemRepository foodItemRepository)
    {
        _foodItemRepository = foodItemRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FoodItemSummary>> Handle(SearchFoodItemsQuery request, CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return Array.Empty<FoodItemSummary>();
        }

        var items = await _foodItemRepository.SearchByNameAsync(query, MaxResults, cancellationToken).ConfigureAwait(false);
        return items.Select(ClinicalRegistryMappings.ToSummary).ToList();
    }
}
