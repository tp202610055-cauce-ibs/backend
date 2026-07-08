using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.ArchiveRecommendation;

/// <summary>
/// Validador estructural del comando <see cref="ArchiveRecommendationCommand"/>.
/// </summary>
public sealed class ArchiveRecommendationCommandValidator : AbstractValidator<ArchiveRecommendationCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ArchiveRecommendationCommandValidator()
    {
        RuleFor(x => x.RecommendationId).NotEmpty();
        RuleFor(x => x.Reason).IsInEnum();
    }
}
