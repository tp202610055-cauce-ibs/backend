using Cauce.Application.Common.Identity;
using FluentValidation;

namespace Cauce.Application.Identity.UseCases.RefreshToken;

/// <summary>
/// Validador del comando de renovación de sesión.
/// </summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();

        RuleFor(x => x.ClientId)
            .NotEmpty()
            .MaximumLength(100)
            .Must(OidcClients.IsKnown)
            .WithMessage("El cliente OIDC indicado no es válido.");
    }
}
