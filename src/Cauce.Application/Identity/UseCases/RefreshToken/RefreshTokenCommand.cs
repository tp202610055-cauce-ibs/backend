using Cauce.Application.Common.Identity;
using Cauce.Application.Identity.UseCases.Login;
using MediatR;

namespace Cauce.Application.Identity.UseCases.RefreshToken;

/// <summary>
/// Comando de renovación de sesión: intercambia un refresh token vigente por un juego de tokens
/// nuevo. Reutiliza <see cref="LoginResult"/> porque el cliente necesita exactamente lo mismo que
/// tras un inicio de sesión, incluida la identidad del usuario.
/// </summary>
/// <param name="RefreshToken">
/// Refresh token vigente. En el portal llega de la cookie y puede venir vacío si el navegador no la
/// envió; el handler lo trata como una sesión inexistente (acta A68).
/// </param>
/// <param name="ClientId">Identificador del cliente OIDC. Tiene que ser el del canal.</param>
/// <param name="Channel">Canal de la petición.</param>
public sealed record RefreshTokenCommand(
    string RefreshToken,
    string ClientId,
    LoginChannel Channel = LoginChannel.Mobile) : IRequest<LoginResult>;
