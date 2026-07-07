using Cauce.Application.ClinicalRegistry.Dtos;

namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Cuerpo de la petición de creación de un alimento personalizado.
/// </summary>
/// <param name="Name">Nombre del alimento personalizado.</param>
/// <param name="PortionSizeGrams">Tamaño de porción en gramos.</param>
/// <param name="Ingredients">Ingredientes del alimento personalizado.</param>
/// <param name="ConfirmedAllergens">
/// Confirma la creación pese a coincidencias con alergias declaradas (US10 CA03). Por defecto
/// <see langword="false"/>: si hay coincidencias sin confirmar, la respuesta es 409 con el detalle.
/// </param>
public sealed record CreateCustomFoodRequest(
    string Name,
    decimal PortionSizeGrams,
    IReadOnlyList<CustomFoodIngredientRequest> Ingredients,
    bool ConfirmedAllergens = false);
