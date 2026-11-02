namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Resultado del cruce de un alimento del catálogo con las alergias declaradas del paciente.
/// </summary>
/// <param name="FoodId">Identificador del alimento consultado.</param>
/// <param name="FoodName">Nombre del alimento.</param>
/// <param name="HasAllergenConflict">Indica si el alimento choca con alguna alergia declarada.</param>
/// <param name="Allergens">Nombres de las alergias declaradas con las que choca, en orden alfabético.</param>
public sealed record FoodAllergenCheckResult(
    Guid FoodId,
    string FoodName,
    bool HasAllergenConflict,
    IReadOnlyList<string> Allergens);
