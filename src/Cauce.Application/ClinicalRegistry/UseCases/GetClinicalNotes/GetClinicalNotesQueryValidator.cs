using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetClinicalNotes;

/// <summary>
/// Validador del rango de fechas de <see cref="GetClinicalNotesQuery"/>.
/// </summary>
public sealed class GetClinicalNotesQueryValidator : AbstractValidator<GetClinicalNotesQuery>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public GetClinicalNotesQueryValidator()
    {
        // El binder solo deja Kind=Utc si el texto trae Z u offset; sin zona, Npgsql rechaza la fecha.
        RuleFor(x => x.From).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("La fecha inicial debe incluir la zona horaria, por ejemplo Z o -05:00.");
        RuleFor(x => x.To).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("La fecha final debe incluir la zona horaria, por ejemplo Z o -05:00.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}
