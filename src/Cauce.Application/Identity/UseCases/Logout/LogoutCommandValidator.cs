using FluentValidation;

namespace Cauce.Application.Identity.UseCases.Logout;

/// <summary>
/// Validador estructural del comando <see cref="LogoutCommand"/>.
/// </summary>
public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(100);
    }
}
