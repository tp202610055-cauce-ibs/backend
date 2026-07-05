using MediatR;

namespace Cauce.Application.Identity.UseCases.Logout;

/// <summary>
/// Comando de cierre de sesión: revoca el refresh token en Keycloak. El evento LOGOUT se audita en
/// el middleware.
/// </summary>
/// <param name="RefreshToken">Refresh token a revocar.</param>
/// <param name="ClientId">Identificador del cliente OIDC.</param>
public sealed record LogoutCommand(string RefreshToken, string ClientId) : IRequest<Unit>;
