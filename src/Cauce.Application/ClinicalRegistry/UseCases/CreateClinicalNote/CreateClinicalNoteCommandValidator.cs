using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateClinicalNote;

/// <summary>
/// Validador estructural del comando <see cref="CreateClinicalNoteCommand"/>. La regla de
/// asociación exclusiva se revalida en la entidad y se reporta con el código
/// <c>invalid_clinical_note_association</c>.
/// </summary>
public sealed class CreateClinicalNoteCommandValidator : AbstractValidator<CreateClinicalNoteCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public CreateClinicalNoteCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("El contenido de la nota es obligatorio.")
            .MaximumLength(500);

        RuleFor(x => x)
            .Must(note => note.MealId.HasValue ^ note.SymptomId.HasValue)
            .WithMessage("La nota debe asociarse exactamente a una comida o a un síntoma.");
    }
}
