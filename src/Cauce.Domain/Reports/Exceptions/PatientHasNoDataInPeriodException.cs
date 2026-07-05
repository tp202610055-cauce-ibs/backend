using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Reports.Exceptions;

/// <summary>
/// Se lanza cuando un paciente no tiene datos clínicos en el período solicitado, por lo que no
/// hay nada que reportar.
/// </summary>
public sealed class PatientHasNoDataInPeriodException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el identificador del paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    public PatientHasNoDataInPeriodException(Guid patientId)
        : base($"El paciente '{patientId}' no tiene datos clínicos en el período solicitado.")
    {
        PatientId = patientId;
    }

    /// <summary>
    /// Identificador del paciente sin datos en el período.
    /// </summary>
    public Guid PatientId { get; }
}
