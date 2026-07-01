using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.ListPendingReviewForNutritionist;

/// <summary>
/// Validador estructural de la consulta <see cref="ListPendingReviewForNutritionistQuery"/>.
/// </summary>
public sealed class ListPendingReviewForNutritionistQueryValidator : AbstractValidator<ListPendingReviewForNutritionistQuery>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ListPendingReviewForNutritionistQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
