using FluentValidation;

namespace Cauce.Application.Identity.UseCases.ResendNutritionistActivationEmail;

/// <summary>
/// Validador del comando <see cref="ResendNutritionistActivationEmailCommand"/>.
/// </summary>
public sealed class ResendNutritionistActivationEmailCommandValidator
    : AbstractValidator<ResendNutritionistActivationEmailCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ResendNutritionistActivationEmailCommandValidator()
    {
        RuleFor(x => x.NutritionistId).NotEmpty();
    }
}
