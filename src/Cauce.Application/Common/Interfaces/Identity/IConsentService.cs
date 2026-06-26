namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Expone el documento de consentimiento informado vigente y verifica la
/// integridad del hash proporcionado por el cliente al registrarse.
/// </summary>
public interface IConsentService
{
    /// <summary>
    /// Devuelve la versión vigente del documento de consentimiento.
    /// </summary>
    /// <returns>Versión vigente.</returns>
    string GetCurrentVersion();

    /// <summary>
    /// Devuelve el texto íntegro del documento de consentimiento vigente.
    /// </summary>
    /// <returns>Texto vigente.</returns>
    string GetCurrentText();

    /// <summary>
    /// Devuelve el hash SHA-256 (hex) del texto del documento vigente.
    /// </summary>
    /// <returns>Hash vigente.</returns>
    string GetCurrentTextHash();

    /// <summary>
    /// Verifica que la versión y el hash proporcionados por el cliente coincidan
    /// con el documento vigente.
    /// </summary>
    /// <param name="documentVersion">Versión declarada por el cliente.</param>
    /// <param name="clientProvidedHash">Hash declarado por el cliente.</param>
    /// <returns><see langword="true"/> si la versión y el hash coinciden con el documento vigente.</returns>
    bool VerifyHash(string documentVersion, string clientProvidedHash);
}
