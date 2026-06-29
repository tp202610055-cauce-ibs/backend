using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.DeleteCustomFood;

/// <summary>
/// Validador estructural del comando <see cref="DeleteCustomFoodCommand"/>.
/// </summary>
public sealed class DeleteCustomFoodCommandValidator : AbstractValidator<DeleteCustomFoodCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public DeleteCustomFoodCommandValidator()
    {
        RuleFor(x => x.CustomFoodId).NotEmpty();
    }
}
