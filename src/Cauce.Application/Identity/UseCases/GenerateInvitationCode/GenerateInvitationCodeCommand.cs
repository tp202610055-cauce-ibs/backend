using Cauce.Application.Common.Interfaces;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.Identity.UseCases.GenerateInvitationCode;

/// <summary>
/// Comando para generar un código de invitación. El identificador proviene de
/// <c>ICurrentUserService.UserId</c>, es decir, el <c>sub</c> (identificador de
/// Keycloak) del nutricionista autenticado, no la clave primaria local.
/// </summary>
/// <param name="NutritionistUserId">Identificador de Keycloak del nutricionista autenticado.</param>
public sealed record GenerateInvitationCodeCommand(
    Guid NutritionistUserId) : IRequest<GenerateInvitationCodeResult>, IAuditableCommand
{
    /// <inheritdoc />
    public string AuditEntityType => nameof(InvitationCode);

    /// <inheritdoc />
    public AuditActionType AuditActionType => AuditActionType.Register;

    /// <inheritdoc />
    public string? AuditAdditionalContext => null;
}
