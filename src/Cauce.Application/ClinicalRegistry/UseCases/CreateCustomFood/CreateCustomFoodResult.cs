namespace Cauce.Application.ClinicalRegistry.UseCases.CreateCustomFood;

/// <summary>
/// Resultado de la creación de un alimento personalizado.
/// </summary>
/// <param name="CustomFoodId">Identificador del alimento personalizado creado.</param>
public sealed record CreateCustomFoodResult(Guid CustomFoodId);
