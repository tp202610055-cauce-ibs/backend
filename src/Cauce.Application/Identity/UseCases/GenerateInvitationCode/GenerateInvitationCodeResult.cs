namespace Cauce.Application.Identity.UseCases.GenerateInvitationCode;

/// <summary>
/// Resultado de la generación de un código de invitación.
/// </summary>
/// <param name="Code">Código generado.</param>
/// <param name="ExpiresAt">Momento de expiración del código, en UTC.</param>
public sealed record GenerateInvitationCodeResult(
    string Code,
    DateTime ExpiresAt);
