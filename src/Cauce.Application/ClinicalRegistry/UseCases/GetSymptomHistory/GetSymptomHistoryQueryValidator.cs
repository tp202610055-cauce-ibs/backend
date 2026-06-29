using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetSymptomHistory;

/// <summary>
/// Validador del rango y la paginación de <see cref="GetSymptomHistoryQuery"/>.
/// </summary>
public sealed class GetSymptomHistoryQueryValidator : AbstractValidator<GetSymptomHistoryQuery>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public GetSymptomHistoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}
