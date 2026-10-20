using FluentValidation;

namespace Cauce.Application.Recommendations.UseCases.ApproveRecommendation;

/// <summary>
/// Validador estructural del comando <see cref="ApproveRecommendationCommand"/>. La nota clínica es
/// obligatoria y tiene entre 10 y 2000 caracteres sin contar los espacios de los extremos.
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
        RuleFor(x => x.Note).ReviewNote("La nota clínica");
    }
}
