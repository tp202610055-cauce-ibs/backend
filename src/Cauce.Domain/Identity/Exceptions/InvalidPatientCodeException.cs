using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando un código de paciente no respeta el formato canónico <c>PAC-0000</c>.
/// </summary>
public sealed class InvalidPatientCodeException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el valor rechazado.
    /// </summary>
    /// <param name="value">Valor que no pudo interpretarse como código de paciente.</param>
    public InvalidPatientCodeException(string? value)
        : base($"El código de paciente '{value}' no respeta el formato PAC-0000.")
    {
    }
}
