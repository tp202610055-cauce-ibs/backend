using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.RejectRecommendation;

/// <summary>
/// Validador estructural del comando <see cref="RejectRecommendationCommand"/>. El motivo del rechazo es
/// obligatorio y tiene entre 10 y 2000 caracteres sin contar los espacios de los extremos.
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
        RuleFor(x => x.Reason).ReviewNote("El motivo de rechazo");
    }
}
