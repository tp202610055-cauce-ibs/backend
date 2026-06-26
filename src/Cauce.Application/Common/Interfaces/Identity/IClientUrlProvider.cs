namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Construye enlaces de cara al cliente a partir de la URL base de la aplicación
/// configurada. Aísla la capa de aplicación de la configuración de infraestructura.
/// </summary>
public interface IClientUrlProvider
{
    /// <summary>
    /// Construye el enlace de restablecimiento de contraseña con el token en claro.
    /// </summary>
    /// <param name="plainToken">Token de restablecimiento en claro.</param>
    /// <returns>El enlace absoluto de restablecimiento.</returns>
    string BuildPasswordResetLink(string plainToken);
}
