using FluentValidation;

namespace Cauce.Application.Identity.UseCases.Login;

/// <summary>
/// Validador estructural del comando <see cref="LoginCommand"/>.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(100);
    }
}
