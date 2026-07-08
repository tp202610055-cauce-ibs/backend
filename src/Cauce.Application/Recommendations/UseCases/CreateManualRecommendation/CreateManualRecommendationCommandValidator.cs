using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.CreateManualRecommendation;

/// <summary>
/// Validador estructural del comando <see cref="CreateManualRecommendationCommand"/>.
/// </summary>
public sealed class CreateManualRecommendationCommandValidator : AbstractValidator<CreateManualRecommendationCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public CreateManualRecommendationCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ClinicalNote)
            .NotEmpty()
            .MinimumLength(10)
            .WithMessage("La nota clínica debe tener al menos 10 caracteres.")
            .MaximumLength(2000);
        RuleForEach(x => x.Steps).NotEmpty().MaximumLength(500).When(x => x.Steps is not null);
    }
}
