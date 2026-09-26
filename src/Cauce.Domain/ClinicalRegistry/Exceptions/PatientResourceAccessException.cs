using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando un paciente intenta acceder a un recurso clínico (comida, síntoma,
/// alimento personalizado, nota o evaluación) que no le pertenece, o cuando un nutricionista
/// intenta vincular recursos de pacientes distintos.
/// </summary>
public sealed class PatientResourceAccessException : DomainException
{
    /// <summary>
    /// Inicializa la excepción.
    /// </summary>
    public PatientResourceAccessException()
        : base("El recurso solicitado no pertenece al paciente autenticado.")
    {
    }

    /// <summary>
    /// Inicializa la excepción con un mensaje propio, para los flujos donde quien actúa no es el paciente
    /// dueño del recurso sino su nutricionista.
    /// </summary>
    /// <param name="message">Mensaje que describe la falta de pertenencia.</param>
    public PatientResourceAccessException(string message)
        : base(message)
    {
    }
}
