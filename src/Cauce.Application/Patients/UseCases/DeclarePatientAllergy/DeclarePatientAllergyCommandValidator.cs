using FluentValidation;

namespace Cauce.Application.Patients.UseCases.DeclarePatientAllergy;

/// <summary>
/// Validador del comando <see cref="DeclarePatientAllergyCommand"/>.
/// </summary>
public sealed class DeclarePatientAllergyCommandValidator : AbstractValidator<DeclarePatientAllergyCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public DeclarePatientAllergyCommandValidator()
    {
        RuleFor(x => x.AllergyId).NotEmpty();
        RuleFor(x => x.Severity).IsInEnum();
        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}
