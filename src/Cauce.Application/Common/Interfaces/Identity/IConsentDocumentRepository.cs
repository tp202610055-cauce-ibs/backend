using Cauce.Domain.Identity;

namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Acceso a las versiones publicadas del documento de consentimiento informado.
/// </summary>
public interface IConsentDocumentRepository
{
    /// <summary>
    /// Busca una versión por su número de versión.
    /// </summary>
    /// <param name="version">Versión del documento, tal como la guardó el registro de aceptación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La versión publicada, o <see langword="null"/> si no existe.</returns>
    Task<ConsentDocument?> FindByVersionAsync(string version, CancellationToken ct = default);

    /// <summary>
    /// Devuelve la versión marcada como vigente.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La versión vigente, o <see langword="null"/> si la tabla está vacía.</returns>
    Task<ConsentDocument?> FindCurrentAsync(CancellationToken ct = default);
}
