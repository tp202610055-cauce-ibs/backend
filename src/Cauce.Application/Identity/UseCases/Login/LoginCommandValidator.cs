using FluentValidation;

namespace Cauce.Application.Identity.UseCases.Login;

/// <summary>
/// Validador estructural del comando <see cref="LoginCommand"/>. Que el cliente corresponda al canal no
/// se valida aquí sino en el handler, porque ese rechazo tiene que quedar en la auditoría con su causa
/// (acta A68).
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
        RuleFor(x => x.Channel).IsInEnum();
    }
}
