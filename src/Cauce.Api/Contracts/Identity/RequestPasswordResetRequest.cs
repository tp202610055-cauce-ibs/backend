namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Cuerpo de la petición de solicitud de restablecimiento de contraseña.
/// </summary>
/// <param name="Email">Correo electrónico de la cuenta.</param>
public sealed record RequestPasswordResetRequest(string Email);
