using Cauce.Application.Reports.Contracts;
using Microsoft.Extensions.Logging;
using ScottPlot;

namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Renderiza la evolución IBS-SSS del paciente como un PNG de línea para embeberlo en el reporte PDF
/// (TS11/US22 CA01). Dibuja el puntaje total (0–500) contra la fecha de cada evaluación y una línea de
/// meta clínica en la línea base menos 50 puntos (respuesta clínicamente significativa). Devuelve
/// <see langword="null"/> si no hay evaluaciones o si la generación falla, en cuyo caso el documento
/// omite el gráfico.
/// </summary>
public sealed class IbsSssChartRenderer
{
    private const int WidthPixels = 720;
    private const int HeightPixels = 340;
    private const int ClinicallySignificantDelta = 50;
    private const int MaxScore = 500;

    private readonly ILogger<IbsSssChartRenderer> _logger;

    /// <summary>
    /// Inicializa el renderer con su logger.
    /// </summary>
    /// <param name="logger">Logger de la categoría del renderer.</param>
    public IbsSssChartRenderer(ILogger<IbsSssChartRenderer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Genera el PNG de la evolución IBS-SSS a partir de las evaluaciones del período.
    /// </summary>
    /// <param name="assessments">Evaluaciones ordenadas por fecha ascendente.</param>
    /// <returns>Los bytes del PNG, o <see langword="null"/> si no hay datos o falla la generación.</returns>
    public byte[]? Render(IReadOnlyList<ReportAssessment> assessments)
    {
        if (assessments.Count == 0)
        {
            return null;
        }

        try
        {
            var xs = assessments
                .Select(assessment => assessment.Date.ToDateTime(TimeOnly.MinValue).ToOADate())
                .ToArray();
            var ys = assessments.Select(assessment => (double)assessment.Score).ToArray();

            var plot = new Plot();

            var series = plot.Add.Scatter(xs, ys);
            series.Color = Colors.Teal;
            series.LineWidth = 2;
            series.MarkerSize = 7;

            // Meta clínica: reducción de 50 puntos respecto de la línea base (la evaluación más antigua).
            var goal = ys[0] - ClinicallySignificantDelta;
            var goalLine = plot.Add.HorizontalLine(goal);
            goalLine.Color = Colors.Orange;
            goalLine.LinePattern = LinePattern.Dashed;
            goalLine.LineWidth = 1;
            goalLine.Text = "Meta (-50 pts)";

            plot.Title("Evolución IBS-SSS");
            plot.XLabel("Fecha");
            plot.YLabel("Puntaje (0–500)");
            plot.Axes.DateTimeTicksBottom();
            plot.Axes.SetLimitsY(0, MaxScore);

            return plot.GetImageBytes(WidthPixels, HeightPixels, ImageFormat.Png);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to render the IBS-SSS evolution chart; the report will omit it.");
            return null;
        }
    }
}
