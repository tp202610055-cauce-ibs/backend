using Cauce.Application.Common.Interfaces;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
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
    string NewPassword) : IRequest, IAuditableCommand
{
    /// <inheritdoc />
    public string AuditEntityType => nameof(User);

    /// <inheritdoc />
    public AuditActionType AuditActionType => AuditActionType.PasswordResetConfirm;

    /// <inheritdoc />
    public string? AuditAdditionalContext => null;

    /// <summary>
    /// Proyección sin secretos para el hash de auditoría: no incluye el token ni la nueva
    /// contraseña, para no persistir credenciales (ni sus hashes) en la bitácora.
    /// </summary>
    public object AuditPayload => new { action = "password_reset_confirm" };
}
