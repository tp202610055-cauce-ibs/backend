using Cauce.Application.ClinicalRegistry.Dtos;

namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Cuerpo de la petición de actualización de un alimento personalizado.
/// </summary>
/// <param name="Name">Nuevo nombre del alimento personalizado.</param>
/// <param name="PortionSizeGrams">Nuevo tamaño de porción en gramos.</param>
/// <param name="Ingredients">Nuevo conjunto de ingredientes.</param>
public sealed record UpdateCustomFoodRequest(
    string Name,
    decimal PortionSizeGrams,
    IReadOnlyList<CustomFoodIngredientRequest> Ingredients);
