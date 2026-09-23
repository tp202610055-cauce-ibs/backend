using Cauce.Application.ClinicalRegistry.Dtos;

namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Cuerpo de la petición de actualización de un alimento personalizado.
/// </summary>
/// <param name="Name">Nuevo nombre del alimento personalizado.</param>
/// <param name="PortionSizeGrams">Nuevo tamaño de porción en gramos.</param>
/// <param name="Ingredients">Nuevo conjunto de ingredientes.</param>
/// <param name="ConfirmedAllergens">
/// Confirma el guardado pese a coincidencias con alergias declaradas (US10 CA03). Por defecto
/// <see langword="false"/>: si hay coincidencias sin confirmar, la respuesta es 409 con el detalle.
/// </param>
public sealed record UpdateCustomFoodRequest(
    string Name,
    decimal PortionSizeGrams,
    IReadOnlyList<CustomFoodIngredientRequest> Ingredients,
    bool ConfirmedAllergens = false);
