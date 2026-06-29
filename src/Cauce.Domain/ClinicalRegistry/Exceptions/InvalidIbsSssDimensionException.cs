using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando una dimensión de la evaluación IBS-SSS o el número de ciclo viola
/// sus rangos válidos.
/// </summary>
public sealed class InvalidIbsSssDimensionException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje descriptivo de la violación.
    /// </summary>
    /// <param name="message">Descripción de la dimensión o regla violada (sin datos clínicos).</param>
    public InvalidIbsSssDimensionException(string message)
        : base(message)
    {
    }
}
