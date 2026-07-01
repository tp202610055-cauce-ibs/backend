using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.SubmitFeedback;

/// <summary>
/// Validador estructural del comando <see cref="SubmitFeedbackCommand"/>. El comentario es
/// opcional, pero si está presente no puede superar los 500 caracteres.
/// </summary>
public sealed class SubmitFeedbackCommandValidator : AbstractValidator<SubmitFeedbackCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public SubmitFeedbackCommandValidator()
    {
        RuleFor(x => x.RecommendationId).NotEmpty();
        RuleFor(x => x.ClientGuid).NotEmpty();
        RuleFor(x => x.Outcome).IsInEnum();
        RuleFor(x => x.Comment).MaximumLength(500).When(x => x.Comment is not null);
    }
}
