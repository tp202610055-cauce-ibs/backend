using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando un paciente no tiene historial clínico suficiente para generar una
/// recomendación, ni siquiera tras expandir la ventana de consumo.
/// </summary>
public sealed class InsufficientClinicalHistoryException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el identificador del paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    public InsufficientClinicalHistoryException(Guid patientId)
        : base($"El paciente '{patientId}' no tiene historial clínico suficiente para generar una recomendación.")
    {
        PatientId = patientId;
    }

    /// <summary>
    /// Identificador del paciente sin historial suficiente.
    /// </summary>
    public Guid PatientId { get; }
}
