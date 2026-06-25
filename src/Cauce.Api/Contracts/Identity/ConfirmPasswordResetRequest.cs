namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Cuerpo de la petición de confirmación de restablecimiento de contraseña.
/// </summary>
/// <param name="Token">Token de restablecimiento recibido por correo.</param>
/// <param name="NewPassword">Nueva contraseña elegida por el usuario.</param>
public sealed record ConfirmPasswordResetRequest(string Token, string NewPassword);
