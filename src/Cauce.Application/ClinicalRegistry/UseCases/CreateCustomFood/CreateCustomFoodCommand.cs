using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateCustomFood;

/// <summary>
/// Comando para crear un alimento personalizado del paciente autenticado.
/// </summary>
/// <param name="Name">Nombre del alimento personalizado (1–150 caracteres).</param>
/// <param name="PortionSizeGrams">Tamaño de porción en gramos (mayor que cero).</param>
/// <param name="Ingredients">Ingredientes del alimento personalizado.</param>
public sealed record CreateCustomFoodCommand(
    string Name,
    decimal PortionSizeGrams,
    IReadOnlyList<CustomFoodIngredientRequest> Ingredients) : IRequest<CreateCustomFoodResult>;
