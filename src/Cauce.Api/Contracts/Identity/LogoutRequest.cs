namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Solicitud de cierre de sesión. El backend revoca el refresh token en Keycloak.
/// </summary>
/// <param name="RefreshToken">Refresh token a revocar.</param>
/// <param name="ClientId">Identificador del cliente OIDC.</param>
public sealed record LogoutRequest(string RefreshToken, string ClientId);
