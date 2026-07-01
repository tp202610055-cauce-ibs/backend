using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.DeliverRecommendation;

/// <summary>
/// Validador estructural del comando <see cref="DeliverRecommendationCommand"/>.
/// </summary>
public sealed class DeliverRecommendationCommandValidator : AbstractValidator<DeliverRecommendationCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public DeliverRecommendationCommandValidator()
    {
        RuleFor(x => x.RecommendationId).NotEmpty();
        RuleFor(x => x.ClientGuid).NotEmpty();
    }
}
