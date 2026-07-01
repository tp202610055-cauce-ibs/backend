using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando se intenta crear un puntaje de confianza fuera del rango [0, 1].
/// </summary>
public sealed class InvalidConfidenceScoreException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el valor inválido recibido.
    /// </summary>
    /// <param name="value">Valor que violó el rango permitido.</param>
    public InvalidConfidenceScoreException(decimal value)
        : base($"El puntaje de confianza debe estar en [0, 1]. Se recibió: {value}.")
    {
        Value = value;
    }

    /// <summary>
    /// Valor que violó el rango permitido.
    /// </summary>
    public decimal Value { get; }
}
