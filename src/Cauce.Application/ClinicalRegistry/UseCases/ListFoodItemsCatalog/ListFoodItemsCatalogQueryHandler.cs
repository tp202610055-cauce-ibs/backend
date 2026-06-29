using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Models;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.ListFoodItemsCatalog;

/// <summary>
/// Handler de la consulta paginada del catálogo de alimentos.
/// </summary>
public sealed class ListFoodItemsCatalogQueryHandler : IRequestHandler<ListFoodItemsCatalogQuery, PagedResult<FoodItemSummary>>
{
    private readonly IFoodItemRepository _foodItemRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    /// <param name="foodItemRepository">Repositorio del catálogo de alimentos.</param>
    public ListFoodItemsCatalogQueryHandler(IFoodItemRepository foodItemRepository)
    {
        _foodItemRepository = foodItemRepository;
    }

    /// <inheritdoc />
    public async Task<PagedResult<FoodItemSummary>> Handle(ListFoodItemsCatalogQuery request, CancellationToken cancellationToken)
    {
        var skip = (request.Page - 1) * request.PageSize;

        var items = await _foodItemRepository
            .ListActiveAsync(skip, request.PageSize, request.Category, request.FodmapLevel, cancellationToken)
            .ConfigureAwait(false);

        var total = await _foodItemRepository
            .CountActiveAsync(request.Category, request.FodmapLevel, cancellationToken)
            .ConfigureAwait(false);

        var summaries = items.Select(ClinicalRegistryMappings.ToSummary).ToList();
        return new PagedResult<FoodItemSummary>(summaries, request.Page, request.PageSize, total);
    }
}
