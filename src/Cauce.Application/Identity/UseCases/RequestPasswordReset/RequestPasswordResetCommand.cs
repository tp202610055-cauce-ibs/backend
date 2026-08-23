using System.Text.Json;
using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Interfaces;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.Identity.UseCases.RequestPasswordReset;

/// <summary>
/// Comando para solicitar el restablecimiento de contraseña. Por seguridad, la
/// respuesta es siempre exitosa, exista o no la cuenta, para no filtrar la
/// existencia de correos registrados.
/// </summary>
/// <param name="Email">Correo electrónico de la cuenta.</param>
/// <param name="IpAddress">Dirección IP de origen, o <see langword="null"/>.</param>
/// <param name="ClientId">
/// Cliente OIDC de origen, que determina el destino del enlace enviado por correo. Si es
/// <see langword="null"/>, el handler asume la app móvil.
/// </param>
public sealed record RequestPasswordResetCommand(
    string Email,
    string? IpAddress,
    string? ClientId = null) : IRequest, IAuditableCommand
{
    /// <inheritdoc />
    public string AuditEntityType => nameof(User);

    /// <inheritdoc />
    public AuditActionType AuditActionType => AuditActionType.PasswordResetRequest;

    /// <inheritdoc />
    public string? AuditAdditionalContext => JsonSerializer.Serialize(new { email = AuditMask.Email(Email) });
}
