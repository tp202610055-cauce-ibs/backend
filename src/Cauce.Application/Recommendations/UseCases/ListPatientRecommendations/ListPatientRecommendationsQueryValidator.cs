using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.ListPatientRecommendations;

/// <summary>
/// Validador estructural de la consulta <see cref="ListPatientRecommendationsQuery"/>.
/// </summary>
public sealed class ListPatientRecommendationsQueryValidator : AbstractValidator<ListPatientRecommendationsQuery>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ListPatientRecommendationsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.FilterStatus).IsInEnum().When(x => x.FilterStatus.HasValue);
    }
}
