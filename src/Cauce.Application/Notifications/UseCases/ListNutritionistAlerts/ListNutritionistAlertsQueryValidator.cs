using FluentValidation;

namespace Cauce.Application.Notifications.UseCases.ListNutritionistAlerts;

/// <summary>
/// Validador de <see cref="ListNutritionistAlertsQuery"/>.
/// </summary>
public sealed class ListNutritionistAlertsQueryValidator : AbstractValidator<ListNutritionistAlertsQuery>
{
    /// <summary>
    /// Tamaño de página máximo.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ListNutritionistAlertsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize).WithMessage($"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
    }
}
