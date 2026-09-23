using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.UpdateCustomFood;

/// <summary>
/// Comando para actualizar un alimento personalizado del paciente autenticado (nombre,
/// tamaño de porción e ingredientes).
/// </summary>
/// <param name="CustomFoodId">Identificador del alimento personalizado.</param>
/// <param name="Name">Nuevo nombre (1–150 caracteres).</param>
/// <param name="PortionSizeGrams">Nuevo tamaño de porción en gramos (mayor que cero).</param>
/// <param name="Ingredients">Nuevo conjunto de ingredientes.</param>
/// <param name="ConfirmedAllergens">
/// Indica que el paciente confirmó explícitamente guardar el alimento pese a que sus ingredientes
/// coinciden con alergias declaradas (US10 CA03). Si es <see langword="false"/> y hay coincidencias,
/// la operación responde 409 con el detalle.
/// </param>
public sealed record UpdateCustomFoodCommand(
    Guid CustomFoodId,
    string Name,
    decimal PortionSizeGrams,
    IReadOnlyList<CustomFoodIngredientRequest> Ingredients,
    bool ConfirmedAllergens = false) : IRequest<UpdateCustomFoodResult>;
