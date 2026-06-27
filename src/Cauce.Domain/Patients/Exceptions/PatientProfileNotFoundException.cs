using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Patients.Exceptions;

/// <summary>
/// Se lanza cuando se solicita un perfil clínico de paciente que no existe.
/// </summary>
public sealed class PatientProfileNotFoundException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public PatientProfileNotFoundException()
        : base("El paciente no tiene un perfil clínico registrado.")
    {
    }
}
