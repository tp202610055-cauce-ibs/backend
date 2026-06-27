using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Patients.Exceptions;

/// <summary>
/// Se lanza cuando un nutricionista intenta acceder a los datos de un paciente con
/// el que no tiene una asignación activa. Se mapea a 403 con el código de error
/// <c>unauthorized_patient_access</c>.
/// </summary>
public sealed class PatientAccessNotAuthorizedException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public PatientAccessNotAuthorizedException()
        : base("No tiene una asignación activa con este paciente.")
    {
    }
}
