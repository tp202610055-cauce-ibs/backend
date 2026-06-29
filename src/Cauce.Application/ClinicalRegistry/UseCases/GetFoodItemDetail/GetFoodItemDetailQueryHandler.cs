using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetFoodItemDetail;

/// <summary>
/// Handler de la consulta del detalle de un alimento del catálogo.
/// </summary>
public sealed class GetFoodItemDetailQueryHandler : IRequestHandler<GetFoodItemDetailQuery, FoodItemDetail>
{
    private readonly IFoodItemRepository _foodItemRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    /// <param name="foodItemRepository">Repositorio del catálogo de alimentos.</param>
    public GetFoodItemDetailQueryHandler(IFoodItemRepository foodItemRepository)
    {
        _foodItemRepository = foodItemRepository;
    }

    /// <inheritdoc />
    public async Task<FoodItemDetail> Handle(GetFoodItemDetailQuery request, CancellationToken cancellationToken)
    {
        var food = await _foodItemRepository.FindByIdAsync(request.FoodId, cancellationToken).ConfigureAwait(false)
            ?? throw new FoodItemNotFoundException(request.FoodId);

        return ClinicalRegistryMappings.ToDetail(food);
    }
}
