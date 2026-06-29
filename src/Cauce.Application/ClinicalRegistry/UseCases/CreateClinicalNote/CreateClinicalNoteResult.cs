namespace Cauce.Application.ClinicalRegistry.UseCases.CreateClinicalNote;

/// <summary>
/// Resultado de la creación de una nota clínica.
/// </summary>
/// <param name="NoteId">Identificador de la nota creada.</param>
public sealed record CreateClinicalNoteResult(Guid NoteId);
