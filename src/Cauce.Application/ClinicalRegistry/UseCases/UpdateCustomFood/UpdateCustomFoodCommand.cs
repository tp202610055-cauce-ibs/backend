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
public sealed record UpdateCustomFoodCommand(
    Guid CustomFoodId,
    string Name,
    decimal PortionSizeGrams,
    IReadOnlyList<CustomFoodIngredientRequest> Ingredients) : IRequest<UpdateCustomFoodResult>;
