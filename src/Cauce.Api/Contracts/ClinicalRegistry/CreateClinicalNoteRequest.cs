namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Cuerpo de la petición de creación de una nota clínica, asociada exactamente a una
/// comida o a un síntoma.
/// </summary>
/// <param name="MealId">Identificador de la comida asociada, o <see langword="null"/>.</param>
/// <param name="SymptomId">Identificador del síntoma asociado, o <see langword="null"/>.</param>
/// <param name="Content">Contenido de la nota (1–500 caracteres).</param>
public sealed record CreateClinicalNoteRequest(Guid? MealId, Guid? SymptomId, string Content);
