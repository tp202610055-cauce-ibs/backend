using Cauce.Application.Identity.UseCases.Login;
using MediatR;

namespace Cauce.Application.Identity.UseCases.RefreshToken;

/// <summary>
/// Comando de renovación de sesión: intercambia un refresh token vigente por un juego de tokens
/// nuevo. Reutiliza <see cref="LoginResult"/> porque el cliente necesita exactamente lo mismo que
/// tras un inicio de sesión, incluida la identidad del usuario.
/// </summary>
/// <param name="RefreshToken">Refresh token vigente.</param>
/// <param name="ClientId">Identificador del cliente OIDC.</param>
public sealed record RefreshTokenCommand(string RefreshToken, string ClientId) : IRequest<LoginResult>;
