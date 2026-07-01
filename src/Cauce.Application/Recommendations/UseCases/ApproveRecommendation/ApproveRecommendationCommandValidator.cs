using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.ApproveRecommendation;

/// <summary>
/// Validador estructural del comando <see cref="ApproveRecommendationCommand"/>. La nota clínica
/// es obligatoria y debe tener entre 10 y 2000 caracteres no vacíos.
/// </summary>
public sealed class ApproveRecommendationCommandValidator : AbstractValidator<ApproveRecommendationCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ApproveRecommendationCommandValidator()
    {
        RuleFor(x => x.RecommendationId).NotEmpty();
        RuleFor(x => x.ClientGuid).NotEmpty();
        RuleFor(x => x.Note)
            .NotEmpty()
            .Must(note => !string.IsNullOrWhiteSpace(note))
            .WithMessage("La nota clínica no puede estar vacía.")
            .MinimumLength(10)
            .MaximumLength(2000);
    }
}
