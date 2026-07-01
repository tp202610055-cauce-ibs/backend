using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.RejectRecommendation;

/// <summary>
/// Validador estructural del comando <see cref="RejectRecommendationCommand"/>. El motivo del
/// rechazo es obligatorio y debe tener entre 10 y 2000 caracteres no vacíos.
/// </summary>
public sealed class RejectRecommendationCommandValidator : AbstractValidator<RejectRecommendationCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public RejectRecommendationCommandValidator()
    {
        RuleFor(x => x.RecommendationId).NotEmpty();
        RuleFor(x => x.ClientGuid).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("El motivo de rechazo no puede estar vacío.")
            .MinimumLength(10)
            .MaximumLength(2000);
    }
}
