using Cauce.Application.ClinicalRegistry.Dtos;
using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateMeal;

/// <summary>
/// Validador estructural del comando <see cref="CreateMealCommand"/>. Las invariantes de
/// dominio (marca temporal futura, número de ítems) se revalidan en la entidad
/// <c>Meal</c> y se reportan con el código <c>invalid_meal_registration</c>.
/// </summary>
public sealed class CreateMealCommandValidator : AbstractValidator<CreateMealCommand>
{
    private const int MaxItems = 50;

    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public CreateMealCommandValidator()
    {
        RuleFor(x => x.ClientGuid).NotEmpty().WithMessage("El client_guid es obligatorio.");
        RuleFor(x => x.MealTime).IsInEnum();
        RuleFor(x => x.Items)
            .NotNull()
            .Must(items => items is { Count: >= 1 and <= MaxItems })
            .WithMessage($"Una comida debe tener entre 1 y {MaxItems} ítems.");

        RuleForEach(x => x.Items).SetValidator(new MealItemRequestValidator());
    }
}

/// <summary>
/// Validador estructural de un <see cref="MealItemRequest"/>.
/// </summary>
public sealed class MealItemRequestValidator : AbstractValidator<MealItemRequest>
{
    /// <summary>
    /// Configura las reglas de validación de un ítem de comida.
    /// </summary>
    public MealItemRequestValidator()
    {
        RuleFor(x => x.Unit).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThan(0m).WithMessage("La cantidad debe ser mayor que cero.");
        RuleFor(x => x)
            .Must(item => item.FoodId.HasValue ^ item.CustomFoodId.HasValue)
            .WithMessage("Cada ítem debe referenciar exactamente un alimento del catálogo o uno personalizado.");
    }
}
