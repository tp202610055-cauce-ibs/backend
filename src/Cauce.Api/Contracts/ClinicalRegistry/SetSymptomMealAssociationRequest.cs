namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Cuerpo de la corrección manual de la comida asociada a un síntoma.
/// </summary>
/// <param name="MealId">Comida que se asocia al síntoma, o <see langword="null"/> para desvincularlo.</param>
public sealed record SetSymptomMealAssociationRequest(Guid? MealId);
