using FluentValidation;

namespace Cauce.Application.Identity.UseCases.CreateNutritionist;

/// <summary>
/// Validador del comando <see cref="CreateNutritionistCommand"/>.
/// </summary>
public sealed class CreateNutritionistCommandValidator : AbstractValidator<CreateNutritionistCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public CreateNutritionistCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(150)
            .EmailAddress();

        RuleFor(x => x.FullName)
            .NotEmpty()
            .Length(2, 150);
    }
}
