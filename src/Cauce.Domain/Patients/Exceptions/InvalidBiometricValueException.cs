using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Patients.Exceptions;

/// <summary>
/// Se lanza cuando un valor biométrico (edad, peso, altura o fecha de diagnóstico)
/// está fuera de los rangos válidos. El mensaje no expone el valor para evitar PII.
/// </summary>
public sealed class InvalidBiometricValueException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con un mensaje que describe el campo afectado sin
    /// revelar el valor.
    /// </summary>
    /// <param name="message">Descripción del campo y rango válido.</param>
    public InvalidBiometricValueException(string message)
        : base(message)
    {
    }
}
