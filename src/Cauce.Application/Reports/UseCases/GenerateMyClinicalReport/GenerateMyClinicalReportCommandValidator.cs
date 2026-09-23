using FluentValidation;

namespace Cauce.Application.Reports.UseCases.GenerateMyClinicalReport;

/// <summary>
/// Validador estructural del comando <see cref="GenerateMyClinicalReportCommand"/>. Las reglas son
/// las mismas que las del reporte del nutricionista: o se omiten los dos extremos del período, o se
/// envían los dos, con inicio anterior al fin, fin no futuro y una duración acotada.
///
/// <para>La cota máxima se compara contra un valor fijo y no contra la configuración porque el
/// validador vive en la capa de aplicación, que no conoce <c>ReportOptions</c>; la configuración
/// gobierna el <b>valor por defecto</b> del período, no el techo, que es una regla de contrato.</para>
/// </summary>
public sealed class GenerateMyClinicalReportCommandValidator : AbstractValidator<GenerateMyClinicalReportCommand>
{
    /// <summary>
    /// Duración máxima, en días, del período solicitable.
    /// </summary>
    public const int MaxPeriodDays = 90;

    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public GenerateMyClinicalReportCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.PeriodStart.HasValue == x.PeriodEnd.HasValue)
            .WithName("period")
            .WithMessage("El período debe enviarse completo (inicio y fin) o no enviarse.");

        When(x => x.PeriodStart.HasValue && x.PeriodEnd.HasValue, () =>
        {
            RuleFor(x => x.PeriodStart!.Value)
                .LessThan(x => x.PeriodEnd!.Value)
                .WithName("periodStart")
                .WithMessage("El inicio del período debe ser anterior al fin.");

            RuleFor(x => x.PeriodEnd!.Value)
                .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
                .WithName("periodEnd")
                .WithMessage("El fin del período no puede ser futuro.");

            RuleFor(x => x)
                .Must(x => x.PeriodEnd!.Value.DayNumber - x.PeriodStart!.Value.DayNumber <= MaxPeriodDays)
                .WithName("period")
                .WithMessage($"El período no puede superar los {MaxPeriodDays} días.");
        });
    }
}
