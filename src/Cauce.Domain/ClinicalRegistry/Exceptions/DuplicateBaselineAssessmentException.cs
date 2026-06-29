using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando un paciente intenta registrar una segunda evaluación IBS-SSS de
/// línea base. Solo puede existir una por paciente.
/// </summary>
public sealed class DuplicateBaselineAssessmentException : DomainException
{
    /// <summary>
    /// Inicializa la excepción.
    /// </summary>
    public DuplicateBaselineAssessmentException()
        : base("El paciente ya tiene una evaluación IBS-SSS de línea base registrada.")
    {
    }
}
