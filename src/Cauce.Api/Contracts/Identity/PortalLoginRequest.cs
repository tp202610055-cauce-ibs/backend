namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Cuerpo de la petición de inicio de sesión del portal web. No lleva cliente OIDC: el canal lo define
/// la ruta, y el backend usa siempre <c>cauce-web-portal</c> (acta A68).
/// </summary>
/// <param name="Email">Correo electrónico.</param>
/// <param name="Password">Contraseña.</param>
public sealed record PortalLoginRequest(string Email, string Password);
