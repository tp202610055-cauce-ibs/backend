using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando se intenta activar una versión de modelo mientras otra ya está activa.
/// </summary>
public sealed class ActiveModelVersionConflictException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el nombre de la versión que no pudo activarse.
    /// </summary>
    /// <param name="versionName">Nombre de la versión que se intentó activar.</param>
    public ActiveModelVersionConflictException(string versionName)
        : base($"No se puede activar '{versionName}' mientras otra versión está activa. Desactívela primero.")
    {
        VersionName = versionName;
    }

    /// <summary>
    /// Nombre de la versión que se intentó activar.
    /// </summary>
    public string VersionName { get; }
}
