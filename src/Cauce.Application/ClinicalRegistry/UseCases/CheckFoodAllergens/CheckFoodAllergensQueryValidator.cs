using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.CheckFoodAllergens;

/// <summary>
/// Validador de <see cref="CheckFoodAllergensQuery"/>.
/// </summary>
public sealed class CheckFoodAllergensQueryValidator : AbstractValidator<CheckFoodAllergensQuery>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public CheckFoodAllergensQueryValidator()
    {
        RuleFor(x => x.FoodId).NotEmpty().WithMessage("El alimento es obligatorio.");
    }
}
