namespace Cauce.Application.ClinicalRegistry.UseCases.UpdateCustomFood;

/// <summary>
/// Resultado de la actualización de un alimento personalizado.
/// </summary>
/// <param name="CustomFoodId">Identificador del alimento personalizado actualizado.</param>
public sealed record UpdateCustomFoodResult(Guid CustomFoodId);
