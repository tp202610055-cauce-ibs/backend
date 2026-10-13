using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetMealHistory;

/// <summary>
/// Validador del rango y la paginación de <see cref="GetMealHistoryQuery"/>.
/// </summary>
public sealed class GetMealHistoryQueryValidator : AbstractValidator<GetMealHistoryQuery>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public GetMealHistoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        // El binder solo deja Kind=Utc si el texto trae Z u offset; sin zona, Npgsql rechaza la fecha.
        RuleFor(x => x.From).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("La fecha inicial debe incluir la zona horaria, por ejemplo Z o -05:00.");
        RuleFor(x => x.To).Must(date => date.Kind == DateTimeKind.Utc).WithMessage("La fecha final debe incluir la zona horaria, por ejemplo Z o -05:00.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}
