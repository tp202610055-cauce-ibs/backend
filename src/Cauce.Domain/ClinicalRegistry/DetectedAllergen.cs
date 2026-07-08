namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Coincidencia entre un ingrediente de un alimento personalizado y una alergia declarada por el
/// paciente (US10 CA03). Se calcula con el matcher heurístico conservador (DEC-B4-14) y se devuelve
/// al cliente para que el paciente confirme o descarte la creación.
/// </summary>
/// <param name="IngredientName">Nombre del alimento del catálogo usado como ingrediente.</param>
/// <param name="AllergenName">Nombre de la alergia declarada que coincide.</param>
/// <param name="Severity">Severidad declarada de la alergia.</param>
public sealed record DetectedAllergen(string IngredientName, string AllergenName, string Severity);
