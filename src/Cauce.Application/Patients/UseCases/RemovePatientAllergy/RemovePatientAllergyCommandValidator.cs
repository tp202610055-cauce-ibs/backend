using FluentValidation;

namespace Cauce.Application.Patients.UseCases.RemovePatientAllergy;

/// <summary>
/// Validador del comando <see cref="RemovePatientAllergyCommand"/>.
/// </summary>
public sealed class RemovePatientAllergyCommandValidator : AbstractValidator<RemovePatientAllergyCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public RemovePatientAllergyCommandValidator()
    {
        RuleFor(x => x.PatientAllergyId).NotEmpty();
    }
}
