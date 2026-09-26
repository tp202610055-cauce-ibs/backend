using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.SetSymptomMealAssociation;

/// <summary>
/// Validador estructural del comando <see cref="SetSymptomMealAssociationCommand"/>. Un
/// <see cref="SetSymptomMealAssociationCommand.MealId"/> nulo es válido y significa desvincular; lo que
/// se rechaza es el GUID vacío, que no identifica ninguna comida.
/// </summary>
public sealed class SetSymptomMealAssociationCommandValidator : AbstractValidator<SetSymptomMealAssociationCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public SetSymptomMealAssociationCommandValidator()
    {
        RuleFor(x => x.SymptomId).NotEmpty();
        RuleFor(x => x.ClientGuid).NotEmpty();
        RuleFor(x => x.MealId)
            .Must(mealId => mealId != Guid.Empty)
            .When(x => x.MealId.HasValue)
            .WithMessage("El identificador de la comida no puede ser vacío; para desvincular, envíe null.");
    }
}
