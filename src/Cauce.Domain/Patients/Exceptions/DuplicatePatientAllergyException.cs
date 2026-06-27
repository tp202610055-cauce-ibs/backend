using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Patients.Exceptions;

/// <summary>
/// Se lanza cuando un paciente intenta declarar una alergia que ya tenía declarada.
/// </summary>
public sealed class DuplicatePatientAllergyException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public DuplicatePatientAllergyException()
        : base("El paciente ya declaró esta alergia.")
    {
    }
}
