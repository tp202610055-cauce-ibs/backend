using MediatR;

namespace Cauce.Application.Identity.UseCases.Login;

/// <summary>
/// Comando de inicio de sesión: hace passthrough a Keycloak con las credenciales del usuario y
/// devuelve los tokens. El backend es la única puerta de entrada, lo que permite auditar el evento
/// LOGIN/FAILED_LOGIN en el middleware.
/// </summary>
/// <param name="Email">Correo electrónico.</param>
/// <param name="Password">Contraseña.</param>
/// <param name="ClientId">Identificador del cliente OIDC (móvil o portal web).</param>
public sealed record LoginCommand(string Email, string Password, string ClientId) : IRequest<LoginResult>;

/// <summary>
/// Resultado del inicio de sesión.
/// </summary>
/// <param name="AccessToken">Token de acceso JWT.</param>
/// <param name="RefreshToken">Token de refresco.</param>
/// <param name="ExpiresIn">Vigencia del token de acceso, en segundos.</param>
/// <param name="RefreshExpiresIn">Vigencia del token de refresco, en segundos.</param>
/// <param name="TokenType">Tipo de token.</param>
public sealed record LoginResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    int RefreshExpiresIn,
    string TokenType);
