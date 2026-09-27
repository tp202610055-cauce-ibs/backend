namespace Cauce.Application.Common.Exceptions;

/// <summary>
/// Se lanza cuando Keycloak rechaza al cliente OIDC que usa el backend (<c>unauthorized_client</c> o
/// <c>invalid_client</c>): falta el secret, es incorrecto o el cliente no admite el grant pedido. Es un
/// error de configuración del servidor, no de las credenciales del usuario, y por eso no se responde
/// como credenciales inválidas (acta A68).
/// </summary>
public sealed class IdentityProviderMisconfiguredException : Exception
{
    /// <summary>
    /// Inicializa la excepción con el cliente y el código de error de Keycloak, para el log interno.
    /// Ninguno de los dos viaja al cliente.
    /// </summary>
    /// <param name="clientId">Cliente OIDC rechazado.</param>
    /// <param name="oauthError">Código de error OAuth 2.0 que devolvió Keycloak.</param>
    public IdentityProviderMisconfiguredException(string clientId, string? oauthError)
        : base("El proveedor de identidad rechazó la configuración del cliente.")
    {
        ClientId = clientId;
        OAuthError = oauthError;
    }

    /// <summary>
    /// Cliente OIDC que Keycloak rechazó.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// Código de error OAuth 2.0 que devolvió Keycloak, o <see langword="null"/> si no lo informó.
    /// </summary>
    public string? OAuthError { get; }
}
