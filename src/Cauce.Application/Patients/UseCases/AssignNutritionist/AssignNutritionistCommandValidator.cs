using FluentValidation;

namespace Cauce.Application.Patients.UseCases.AssignNutritionist;

/// <summary>
/// Validador del comando <see cref="AssignNutritionistCommand"/>. Replica las reglas de formato que ya
/// aplica el registro al código de invitación (<c>RegisterPatientCommandValidator</c>), para que un
/// mismo código sea válido o inválido igual en los dos flujos (regla R10).
/// </summary>
public sealed class AssignNutritionistCommandValidator : AbstractValidator<AssignNutritionistCommand>
{
    private const int MinCodeLength = 8;
    private const int MaxCodeLength = 20;

    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public AssignNutritionistCommandValidator()
    {
        RuleFor(x => x.InvitationCode)
            .NotEmpty().WithMessage("El código de invitación es obligatorio.")
            .Length(MinCodeLength, MaxCodeLength)
                .WithMessage($"El código de invitación debe tener entre {MinCodeLength} y {MaxCodeLength} caracteres.")
            .Matches("^[A-Z0-9]+$")
                .WithMessage("El código de invitación debe ser alfanumérico en mayúsculas.");
    }
}
