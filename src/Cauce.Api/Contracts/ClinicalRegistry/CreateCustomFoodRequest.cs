using Cauce.Application.ClinicalRegistry.Dtos;

namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Cuerpo de la petición de creación de un alimento personalizado.
/// </summary>
/// <param name="Name">Nombre del alimento personalizado.</param>
/// <param name="PortionSizeGrams">Tamaño de porción en gramos.</param>
/// <param name="Ingredients">Ingredientes del alimento personalizado.</param>
public sealed record CreateCustomFoodRequest(
    string Name,
    decimal PortionSizeGrams,
    IReadOnlyList<CustomFoodIngredientRequest> Ingredients);
