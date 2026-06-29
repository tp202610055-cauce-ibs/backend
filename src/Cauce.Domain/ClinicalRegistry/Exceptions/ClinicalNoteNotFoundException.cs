using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando se referencia una nota clínica que no existe.
/// </summary>
public sealed class ClinicalNoteNotFoundException : DomainException
{
    /// <summary>
    /// Identificador de la nota clínica no encontrada.
    /// </summary>
    public Guid NoteId { get; }

    /// <summary>
    /// Inicializa la excepción con el identificador de la nota.
    /// </summary>
    /// <param name="noteId">Identificador de la nota clínica.</param>
    public ClinicalNoteNotFoundException(Guid noteId)
        : base($"La nota clínica {noteId} no existe.")
    {
        NoteId = noteId;
    }
}
