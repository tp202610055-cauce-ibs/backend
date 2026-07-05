namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Solicitud de inicio de sesión. El backend hace passthrough a Keycloak con estas credenciales.
/// </summary>
/// <param name="Email">Correo electrónico del usuario.</param>
/// <param name="Password">Contraseña.</param>
/// <param name="ClientId">Identificador del cliente OIDC (por ejemplo, <c>cauce-mobile</c> o <c>cauce-web-portal</c>).</param>
public sealed record LoginRequest(string Email, string Password, string ClientId);
