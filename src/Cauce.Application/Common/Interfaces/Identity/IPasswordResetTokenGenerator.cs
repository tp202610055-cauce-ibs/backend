namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Genera tokens de restablecimiento de contraseña criptográficamente seguros.
/// </summary>
public interface IPasswordResetTokenGenerator
{
    /// <summary>
    /// Genera un par compuesto por el token en claro y su hash SHA-256. El token
    /// en claro se envía por correo; el hash se persiste para verificación posterior.
    /// </summary>
    /// <returns>Una tupla con el token en claro y su hash SHA-256 en hexadecimal.</returns>
    (string PlainToken, string TokenHash) GeneratePair();
}
