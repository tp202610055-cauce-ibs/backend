namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Cliente del endpoint de tokens de Keycloak. Permite el passthrough de inicio de sesión
/// (<c>grant_type=password</c>) y el cierre de sesión (revocación del refresh token), de modo que
/// el backend sea la única puerta de entrada y pueda auditar LOGIN/LOGOUT (DEC-B5-01, acta A3).
/// </summary>
public interface IKeycloakTokenClient
{
    /// <summary>
    /// Solicita tokens a Keycloak con las credenciales del usuario.
    /// </summary>
    /// <param name="email">Correo electrónico (username del realm).</param>
    /// <param name="password">Contraseña.</param>
    /// <param name="clientId">Identificador del cliente OIDC (móvil o portal web).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los tokens emitidos.</returns>
    /// <exception cref="Cauce.Domain.Identity.Exceptions.InvalidCredentialsException">Si las credenciales son inválidas.</exception>
    Task<KeycloakTokenResult> LoginAsync(string email, string password, string clientId, CancellationToken ct = default);

    /// <summary>
    /// Revoca el refresh token del usuario (cierre de sesión).
    /// </summary>
    /// <param name="refreshToken">Refresh token a revocar.</param>
    /// <param name="clientId">Identificador del cliente OIDC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task LogoutAsync(string refreshToken, string clientId, CancellationToken ct = default);

    /// <summary>
    /// Renueva los tokens a partir de un refresh token vigente (<c>grant_type=refresh_token</c>). El
    /// realm tiene rotación activada, así que la respuesta trae un refresh token nuevo y el anterior
    /// queda revocado: el cliente debe persistir el nuevo.
    /// </summary>
    /// <param name="refreshToken">Refresh token vigente.</param>
    /// <param name="clientId">Identificador del cliente OIDC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los tokens renovados.</returns>
    /// <exception cref="Cauce.Domain.Identity.Exceptions.InvalidRefreshTokenException">
    /// Si el refresh token expiró, fue revocado o ya se consumió.
    /// </exception>
    Task<KeycloakTokenResult> RefreshAsync(string refreshToken, string clientId, CancellationToken ct = default);
}

/// <summary>
/// Tokens emitidos por Keycloak.
/// </summary>
/// <param name="AccessToken">Token de acceso JWT.</param>
/// <param name="RefreshToken">Token de refresco.</param>
/// <param name="ExpiresIn">Vigencia del token de acceso, en segundos.</param>
/// <param name="RefreshExpiresIn">Vigencia del token de refresco, en segundos.</param>
/// <param name="TokenType">Tipo de token (por ejemplo, <c>Bearer</c>).</param>
/// <param name="Subject">
/// Identificador del usuario en Keycloak, leído del claim <c>sub</c> del access token. Permite
/// resolver la cuenta local en flujos que no llevan el correo en la petición, como la renovación.
/// </param>
public sealed record KeycloakTokenResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    int RefreshExpiresIn,
    string TokenType,
    string? Subject = null);
