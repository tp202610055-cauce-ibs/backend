using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.GenerateRecommendation;

/// <summary>
/// Validador estructural del comando <see cref="GenerateRecommendationCommand"/>.
/// </summary>
public sealed class GenerateRecommendationCommandValidator : AbstractValidator<GenerateRecommendationCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public GenerateRecommendationCommandValidator()
    {
        RuleFor(x => x.ClientGuid).NotEmpty();
    }
}
