using FluentValidation;

namespace Cauce.Application.Reports.UseCases.GenerateClinicalReport;

/// <summary>
/// Validador estructural del comando <see cref="GenerateClinicalReportCommand"/>. El período debe
/// ser válido, no superar los 90 días y no ser futuro.
/// </summary>
public sealed class GenerateClinicalReportCommandValidator : AbstractValidator<GenerateClinicalReportCommand>
{
    private const int MaxPeriodDays = 90;

    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public GenerateClinicalReportCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.ClientGuid).NotEmpty();
        RuleFor(x => x.PeriodStart).LessThan(x => x.PeriodEnd);
        RuleFor(x => x.PeriodEnd)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("El fin del período no puede ser futuro.");
        RuleFor(x => x)
            .Must(x => x.PeriodEnd.DayNumber - x.PeriodStart.DayNumber <= MaxPeriodDays)
            .WithMessage($"El período no puede superar los {MaxPeriodDays} días.");
    }
}
