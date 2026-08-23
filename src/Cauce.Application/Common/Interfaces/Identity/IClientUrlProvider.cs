namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Construye enlaces de cara al cliente a partir de las URL base configuradas. Aísla la capa de
/// aplicación de la configuración de infraestructura.
/// </summary>
public interface IClientUrlProvider
{
    /// <summary>
    /// Construye el enlace de restablecimiento de contraseña con el token en claro, dirigido al
    /// cliente que originó la solicitud.
    /// </summary>
    /// <param name="plainToken">Token de restablecimiento en claro.</param>
    /// <param name="clientId">Identificador del cliente OIDC de origen.</param>
    /// <returns>El enlace absoluto de restablecimiento.</returns>
    /// <exception cref="ArgumentException">Si el cliente OIDC no es conocido.</exception>
    string BuildPasswordResetLink(string plainToken, string clientId);
}
