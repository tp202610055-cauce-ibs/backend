using Cauce.Application.Common.Identity;
using FluentValidation;

namespace Cauce.Application.Identity.UseCases.Logout;

/// <summary>
/// Validador estructural del comando <see cref="LogoutCommand"/>. Que el cliente corresponda al canal se
/// decide en el handler, para que el rechazo quede auditado con su causa (acta A68).
/// </summary>
public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().When(x => x.Channel == LoginChannel.Mobile);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Channel).IsInEnum();
    }
}
