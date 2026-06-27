using FluentValidation;

namespace Cauce.Application.Patients.UseCases.CreatePatientProfile;

/// <summary>
/// Validador estructural del comando <see cref="CreatePatientProfileCommand"/>. Las
/// invariantes biométricas (rangos de peso, estatura y edad) se validan en la
/// entidad de dominio para que se reporten con el código <c>invalid_biometric_value</c>.
/// </summary>
public sealed class CreatePatientProfileCommandValidator : AbstractValidator<CreatePatientProfileCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public CreatePatientProfileCommandValidator()
    {
        RuleFor(x => x.BiologicalSex).IsInEnum();
        RuleFor(x => x.IbsSubtype).IsInEnum();
        RuleFor(x => x.DateOfBirth)
            .NotEqual(default(DateOnly)).WithMessage("La fecha de nacimiento es obligatoria.");
    }
}
