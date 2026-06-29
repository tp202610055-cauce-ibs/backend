using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateSymptom;

/// <summary>
/// Validador estructural del comando <see cref="CreateSymptomCommand"/>.
/// </summary>
public sealed class CreateSymptomCommandValidator : AbstractValidator<CreateSymptomCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public CreateSymptomCommandValidator()
    {
        RuleFor(x => x.ClientGuid).NotEmpty().WithMessage("El client_guid es obligatorio.");
        RuleFor(x => x.SymptomType).IsInEnum();
        RuleFor(x => x.Intensity)
            .InclusiveBetween(1, 100)
            .WithMessage("La intensidad debe estar entre 1 y 100.");
    }
}
