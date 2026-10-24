using FluentValidation;

namespace Cauce.Application.Identity.UseCases.UpdateFcmToken;

/// <summary>
/// Validador del comando <see cref="UpdateFcmTokenCommand"/>. El token es opcional (nulo desvincula) y no
/// supera el largo de la columna <c>users.fcm_token</c>.
/// </summary>
public sealed class UpdateFcmTokenCommandValidator : AbstractValidator<UpdateFcmTokenCommand>
{
    /// <summary>
    /// Largo máximo del token, igual al de la columna.
    /// </summary>
    public const int MaxTokenLength = 500;

    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public UpdateFcmTokenCommandValidator()
    {
        RuleFor(x => x.FcmToken).MaximumLength(MaxTokenLength);
    }
}
