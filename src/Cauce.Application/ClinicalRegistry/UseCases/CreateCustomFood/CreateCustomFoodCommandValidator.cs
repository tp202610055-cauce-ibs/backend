using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateCustomFood;

/// <summary>
/// Validador estructural del comando <see cref="CreateCustomFoodCommand"/>.
/// </summary>
public sealed class CreateCustomFoodCommandValidator : AbstractValidator<CreateCustomFoodCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public CreateCustomFoodCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.PortionSizeGrams).GreaterThan(0m);

        RuleFor(x => x.Ingredients)
            .NotNull()
            .Must(ingredients => ingredients is { Count: >= 1 })
            .WithMessage("El alimento personalizado debe tener al menos un ingrediente.")
            .Must(ingredients => ingredients.Select(i => i.FoodId).Distinct().Count() == ingredients.Count)
            .WithMessage("No se admiten ingredientes con alimentos repetidos.");

        RuleForEach(x => x.Ingredients).ChildRules(ingredient =>
        {
            ingredient.RuleFor(i => i.FoodId).NotEmpty();
            ingredient.RuleFor(i => i.ProportionGrams).GreaterThan(0m);
        });
    }
}
