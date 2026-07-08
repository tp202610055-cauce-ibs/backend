using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.ModifyRecommendation;

/// <summary>
/// Validador estructural del comando <see cref="ModifyRecommendationCommand"/>.
/// </summary>
public sealed class ModifyRecommendationCommandValidator : AbstractValidator<ModifyRecommendationCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ModifyRecommendationCommandValidator()
    {
        RuleFor(x => x.RecommendationId).NotEmpty();
        RuleFor(x => x.ClinicalNote)
            .NotEmpty()
            .MinimumLength(10)
            .WithMessage("La nota clínica debe tener al menos 10 caracteres.")
            .MaximumLength(2000);
        RuleFor(x => x.Title).MaximumLength(200);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.FoodId).NotEmpty();
        }).When(x => x.Items is not null);
    }
}
