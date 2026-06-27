using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Patients.Exceptions;

/// <summary>
/// Se lanza cuando un usuario intenta crear un segundo perfil clínico de paciente.
/// </summary>
public sealed class DuplicatePatientProfileException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public DuplicatePatientProfileException()
        : base("El paciente ya tiene un perfil clínico registrado.")
    {
    }
}
