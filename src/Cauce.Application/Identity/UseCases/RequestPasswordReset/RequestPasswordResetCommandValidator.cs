using FluentValidation;

namespace Cauce.Application.Identity.UseCases.RequestPasswordReset;

/// <summary>
/// Validador del comando <see cref="RequestPasswordResetCommand"/>.
/// </summary>
public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(150)
            .EmailAddress();
    }
}
