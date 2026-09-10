using System.Text.Json;
using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Interfaces;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.Identity.UseCases.ResendVerificationEmail;

/// <summary>
/// Comando para reenviar el correo de verificación de una cuenta (acta A40). Por seguridad la respuesta
/// es siempre exitosa cuando la petición supera la validación, exista o no la cuenta y esté o no
/// verificada, para no filtrar ni la existencia de correos registrados ni su estado de verificación
/// (decisión D4).
/// </summary>
/// <param name="Email">Correo electrónico de la cuenta, ya normalizado por el controlador.</param>
public sealed record ResendVerificationEmailCommand(string Email) : IRequest, IAuditableCommand
{
    /// <inheritdoc />
    public string AuditEntityType => nameof(User);

    /// <inheritdoc />
    public AuditActionType AuditActionType => AuditActionType.VerificationEmailResendRequest;

    /// <inheritdoc />
    public string? AuditAdditionalContext => JsonSerializer.Serialize(new { email = AuditMask.Email(Email) });
}
