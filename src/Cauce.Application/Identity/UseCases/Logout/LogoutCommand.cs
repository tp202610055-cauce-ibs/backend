using Cauce.Application.Common.Identity;
using MediatR;

namespace Cauce.Application.Identity.UseCases.Logout;

/// <summary>
/// Comando de cierre de sesión: revoca el refresh token en Keycloak. El evento LOGOUT se audita en
/// el middleware.
/// </summary>
/// <param name="RefreshToken">
/// Refresh token a revocar. En el portal llega de la cookie y puede venir vacío si ya no existe: no hay
/// nada que revocar y el cierre igual se completa (acta A68).
/// </param>
/// <param name="ClientId">Identificador del cliente OIDC. Tiene que ser el del canal.</param>
/// <param name="Channel">Canal de la petición.</param>
public sealed record LogoutCommand(
    string RefreshToken,
    string ClientId,
    LoginChannel Channel = LoginChannel.Mobile) : IRequest<Unit>;
