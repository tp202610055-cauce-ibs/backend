using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateIbsSssAssessment;

/// <summary>
/// Validador estructural del comando <see cref="CreateIbsSssAssessmentCommand"/>. Cada
/// dimensión debe estar en el rango [0, 100]; la entidad revalida y reporta con el
/// código <c>invalid_ibs_sss_dimension</c>.
/// </summary>
public sealed class CreateIbsSssAssessmentCommandValidator : AbstractValidator<CreateIbsSssAssessmentCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public CreateIbsSssAssessmentCommandValidator()
    {
        RuleFor(x => x.AssessmentType).IsInEnum();
        RuleFor(x => x.PainSeverity).InclusiveBetween(0, 100);
        RuleFor(x => x.PainFrequency).InclusiveBetween(0, 100);
        RuleFor(x => x.BloatingSeverity).InclusiveBetween(0, 100);
        RuleFor(x => x.BowelHabitsDissatisfaction).InclusiveBetween(0, 100);
        RuleFor(x => x.LifeInterference).InclusiveBetween(0, 100);
    }
}
