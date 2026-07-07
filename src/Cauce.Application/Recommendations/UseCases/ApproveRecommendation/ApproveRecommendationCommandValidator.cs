using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.ApproveRecommendation;

/// <summary>
/// Validador estructural del comando <see cref="ApproveRecommendationCommand"/>. La nota clínica
/// es obligatoria y debe tener entre 20 y 2000 caracteres no vacíos (US17 CA01).
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
            .MinimumLength(20)
            .WithMessage("La nota clínica de aprobación debe tener al menos 20 caracteres.")
            .MaximumLength(2000);
    }
}
