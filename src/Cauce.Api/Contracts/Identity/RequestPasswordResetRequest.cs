namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Cuerpo de la petición de solicitud de restablecimiento de contraseña.
/// </summary>
/// <param name="Email">Correo electrónico de la cuenta.</param>
/// <param name="ClientId">
/// Identificador del cliente OIDC de origen (<c>cauce-mobile</c> o <c>cauce-web-portal</c>), que
/// determina el destino del enlace enviado por correo. Es opcional por compatibilidad con los
/// clientes que ya consumen este endpoint: si se omite, se asume <c>cauce-mobile</c>.
/// </param>
public sealed record RequestPasswordResetRequest(string Email, string? ClientId = null);
