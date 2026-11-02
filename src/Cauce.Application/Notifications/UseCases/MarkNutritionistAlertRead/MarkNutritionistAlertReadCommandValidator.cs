using FluentValidation;

namespace Cauce.Application.Notifications.UseCases.MarkNutritionistAlertRead;

/// <summary>
/// Validador de <see cref="MarkNutritionistAlertReadCommand"/>.
/// </summary>
public sealed class MarkNutritionistAlertReadCommandValidator : AbstractValidator<MarkNutritionistAlertReadCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public MarkNutritionistAlertReadCommandValidator()
    {
        RuleFor(x => x.AlertId).NotEmpty().WithMessage("El aviso es obligatorio.");
    }
}
