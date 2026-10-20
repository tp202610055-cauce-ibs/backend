using FluentValidation;

namespace Cauce.Application.Reports.UseCases.GenerateClinicalReport;

/// <summary>
/// Validador estructural del comando <see cref="GenerateClinicalReportCommand"/>. El inicio del período
/// no puede ser posterior al fin y el fin no puede ser futuro; la duración no tiene tope.
/// </summary>
public sealed class GenerateClinicalReportCommandValidator : AbstractValidator<GenerateClinicalReportCommand>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public GenerateClinicalReportCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.ClientGuid).NotEmpty();
        RuleFor(x => x.PeriodStart)
            .LessThanOrEqualTo(x => x.PeriodEnd)
            .WithMessage("El inicio del período no puede ser posterior al fin.");
        RuleFor(x => x.PeriodEnd)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("El fin del período no puede ser futuro.");
    }
}
