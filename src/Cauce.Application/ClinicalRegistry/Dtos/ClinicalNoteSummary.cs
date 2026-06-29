namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Nota clínica del historial del paciente.
/// </summary>
/// <param name="NoteId">Identificador de la nota.</param>
/// <param name="MealId">Identificador de la comida asociada, o <see langword="null"/>.</param>
/// <param name="SymptomId">Identificador del síntoma asociado, o <see langword="null"/>.</param>
/// <param name="Content">Contenido de la nota.</param>
/// <param name="CreatedAt">Momento de creación, en UTC.</param>
public sealed record ClinicalNoteSummary(
    Guid NoteId,
    Guid? MealId,
    Guid? SymptomId,
    string Content,
    DateTime CreatedAt);
