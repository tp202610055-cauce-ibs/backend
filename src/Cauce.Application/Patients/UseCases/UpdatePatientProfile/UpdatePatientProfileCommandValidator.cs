using FluentValidation;

namespace Cauce.Application.Patients.UseCases.UpdatePatientProfile;

/// <summary>
/// Validador estructural del comando <see cref="UpdatePatientProfileCommand"/>. Los
/// rangos biométricos se validan en la entidad de dominio.
/// </summary>
public sealed class UpdatePatientProfileCommandValidator : AbstractValidator<UpdatePatientProfileCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public UpdatePatientProfileCommandValidator()
    {
        RuleFor(x => x.IbsSubtype!.Value)
            .IsInEnum()
            .When(x => x.IbsSubtype.HasValue);
    }
}
