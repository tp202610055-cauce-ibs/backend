using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando una nota clínica no cumple la regla de asociación exclusiva: debe
/// referirse exactamente a una comida o a un síntoma, nunca a ambos ni a ninguno.
/// </summary>
public sealed class InvalidClinicalNoteAssociationException : DomainException
{
    /// <summary>
    /// Inicializa la excepción.
    /// </summary>
    public InvalidClinicalNoteAssociationException()
        : base("Una nota clínica debe asociarse exactamente a una comida o a un síntoma, no a ambos ni a ninguno.")
    {
    }
}
