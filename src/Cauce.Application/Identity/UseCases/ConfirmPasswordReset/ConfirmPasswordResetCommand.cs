using MediatR;

namespace Cauce.Application.Identity.UseCases.ConfirmPasswordReset;

/// <summary>
/// Comando para confirmar el restablecimiento de contraseña con el token recibido
/// por correo y la nueva contraseña elegida.
/// </summary>
/// <param name="Token">Token de restablecimiento en claro.</param>
/// <param name="NewPassword">Nueva contraseña elegida por el usuario.</param>
public sealed record ConfirmPasswordResetCommand(
    string Token,
    string NewPassword) : IRequest;
