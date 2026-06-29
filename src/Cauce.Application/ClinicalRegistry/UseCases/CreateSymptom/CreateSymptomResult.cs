namespace Cauce.Application.ClinicalRegistry.UseCases.CreateSymptom;

/// <summary>
/// Resultado del registro de un síntoma.
/// </summary>
/// <param name="SymptomId">Identificador del síntoma creado.</param>
/// <param name="AssociatedMealId">Identificador de la comida asociada, o <see langword="null"/>.</param>
/// <param name="HasMealAssociation">Indica si se asoció con una comida.</param>
public sealed record CreateSymptomResult(Guid SymptomId, Guid? AssociatedMealId, bool HasMealAssociation);
