using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.ListPatientRecommendationsForNutritionist;

/// <summary>
/// Validador de la paginación de <see cref="ListPatientRecommendationsForNutritionistQuery"/>.
/// </summary>
public sealed class ListPatientRecommendationsForNutritionistQueryValidator
    : AbstractValidator<ListPatientRecommendationsForNutritionistQuery>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ListPatientRecommendationsForNutritionistQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
