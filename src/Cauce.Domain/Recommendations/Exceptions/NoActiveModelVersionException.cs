using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando no existe ninguna versión de modelo activa al momento de generar una
/// recomendación.
/// </summary>
public sealed class NoActiveModelVersionException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con su mensaje estándar.
    /// </summary>
    public NoActiveModelVersionException()
        : base("No hay una versión de modelo activa para generar recomendaciones.")
    {
    }
}
