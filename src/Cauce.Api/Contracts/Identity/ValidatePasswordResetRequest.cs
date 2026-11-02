namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Cuerpo de la petición que comprueba si un enlace de restablecimiento todavía sirve.
/// </summary>
/// <param name="Token">Token de restablecimiento recibido por correo.</param>
public sealed record ValidatePasswordResetRequest(string? Token);
