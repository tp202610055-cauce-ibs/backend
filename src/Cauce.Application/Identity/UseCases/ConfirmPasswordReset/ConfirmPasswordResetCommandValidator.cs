using FluentValidation;

namespace Cauce.Application.Identity.UseCases.ConfirmPasswordReset;

/// <summary>
/// Validador del comando <see cref="ConfirmPasswordResetCommand"/>. La política de
/// la nueva contraseña es la misma que en el registro de paciente.
/// </summary>
public sealed class ConfirmPasswordResetCommandValidator : AbstractValidator<ConfirmPasswordResetCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ConfirmPasswordResetCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("La contraseña debe contener al menos una letra mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe contener al menos una letra minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe contener al menos un dígito.");
    }
}
