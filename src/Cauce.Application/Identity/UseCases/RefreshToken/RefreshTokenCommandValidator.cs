using Cauce.Application.Common.Identity;
using FluentValidation;

namespace Cauce.Application.Identity.UseCases.RefreshToken;

/// <summary>
/// Validador del comando de renovación de sesión. Que el cliente corresponda al canal se decide en el
/// handler, que audita ese rechazo con su causa (acta A68). Por la misma razón, en el portal no se exige
/// aquí el refresh token: la cookie ausente es un intento de renovación que también queda auditado.
/// </summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().When(x => x.Channel == LoginChannel.Mobile);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Channel).IsInEnum();
    }
}
