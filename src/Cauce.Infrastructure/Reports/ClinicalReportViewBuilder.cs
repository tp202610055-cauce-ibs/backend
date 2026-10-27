using System.Globalization;
using Cauce.Application.Reports.Contracts;

namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Redacta el contenido del reporte a partir de sus datos: textos en español de Perú, fechas
/// dd/MM/yyyy y horas de Lima. No dibuja; eso lo hace <see cref="ClinicalReportDocument"/>.
/// </summary>
internal static class ClinicalReportViewBuilder
{
    private const string DateFormat = "dd/MM/yyyy";
    private const string DateTimeFormat = "dd/MM/yyyy HH:mm";
    private static readonly CultureInfo Peru = CultureInfo.GetCultureInfo("es-PE");

    public static ClinicalReportView Build(ClinicalReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var header = new List<string>
        {
            $"Paciente: {data.PatientCode}   ·   Iniciales: {data.PatientInitials}",
            $"Edad: {data.PatientAge} años   ·   Subtipo: {ReportLabels.IbsSubtype(data.IbsSubtype)}",
            $"Período: {Date(data.PeriodStart)} al {Date(data.PeriodEnd)}   ·   " +
            $"Generado: {DateTime(data.GeneratedAt)} (hora de Lima)"
        };
        if (!string.IsNullOrWhiteSpace(data.NutritionistName))
        {
            header.Add($"Nutricionista: {data.NutritionistName}");
        }

        return new ClinicalReportView(header,
        [
            Profile(data),
            Allergies(data),
            Frequency(data),
            Meals(data),
            Symptoms(data),
            Assessments(data),
            Recommendations(data),
            Feedback(data)
        ]);
    }

    private static ReportViewSection Profile(ClinicalReportData data) => new("Perfil clínico",
    [
        Text(string.Format(Peru, "Peso: {0:0.#} kg   ·   Estatura: {1:0.#} cm", data.WeightKg, data.HeightCm)),
        Text(string.IsNullOrWhiteSpace(data.Medications) ? "Medicación: no declarada" : $"Medicación: {data.Medications}")
    ]);

    private static ReportViewSection Allergies(ClinicalReportData data) => new("Alergias declaradas",
        data.Allergies.Count == 0
            ? [Text("Sin alergias declaradas.")]
            : data.Allergies.Select(a => Bullet($"{a.Name} (severidad {ReportLabels.AllergySeverity(a.Severity)})")).ToList());

    private static ReportViewSection Frequency(ClinicalReportData data)
    {
        var lines = new List<ReportViewLine>
        {
            Bullet($"{data.DaysWithRecords} {(data.DaysWithRecords == 1 ? "día" : "días")} con registros de {data.PeriodDays}"),
            Bullet($"Comidas registradas: {data.MealCount}")
        };
        if (data.FrequentFoods.Count > 0)
        {
            lines.Add(new ReportViewLine("Alimentos más frecuentes:", ReportLineKind.Subheading));
            lines.AddRange(data.FrequentFoods.Select(food => Bullet($"{food.FoodName}: {food.Count}")));
        }

        return new ReportViewSection("Frecuencia de registro", lines);
    }

    private static ReportViewSection Meals(ClinicalReportData data)
    {
        if (data.Meals.Count == 0)
        {
            return new ReportViewSection("Historial de comidas", [Text("Sin comidas registradas en el período.")]);
        }

        var lines = data.Meals
            .Select(meal => Bullet(
                $"{DateTime(meal.ConsumedAt)} · {ReportLabels.MealTime(meal.MealTime)}: " +
                (meal.Items.Count == 0 ? "sin alimentos detallados" : string.Join(", ", meal.Items))))
            .ToList();
        if (data.Meals.Count < data.MealCount)
        {
            lines.Add(new ReportViewLine(
                $"Se listan las primeras {data.Meals.Count} de {data.MealCount} comidas del período.", ReportLineKind.Note));
        }

        return new ReportViewSection("Historial de comidas", lines);
    }

    private static ReportViewSection Symptoms(ClinicalReportData data)
    {
        if (data.Symptoms.Count == 0)
        {
            return new ReportViewSection("Síntomas", [Text("Sin síntomas registrados en el período.")]);
        }

        var lines = data.Symptoms
            .Select(s => Bullet(string.Format(Peru,
                "{0}: {1} {2}   ·   intensidad media {3:0.#}/100 (mín. {4}, máx. {5})",
                ReportLabels.SymptomType(s.SymptomType), s.Count, s.Count == 1 ? "episodio" : "episodios",
                s.AverageIntensity, s.MinIntensity, s.MaxIntensity)))
            .ToList();
        if (data.SymptomEntries.Count > 0)
        {
            lines.Add(new ReportViewLine("Detalle cronológico:", ReportLineKind.Subheading));
            lines.AddRange(data.SymptomEntries.Select(entry => Bullet(
                $"{DateTime(entry.OccurredAt)} · {ReportLabels.SymptomType(entry.SymptomType)}, intensidad {entry.Intensity}/100" +
                (entry.AssociatedWithMeal ? " · asociado a una comida" : string.Empty))));
        }

        return new ReportViewSection("Síntomas", lines);
    }

    private static ReportViewSection Assessments(ClinicalReportData data) => data.Assessments.Count == 0
        ? new ReportViewSection("Evaluaciones IBS-SSS", [Text("Sin evaluaciones en el período.")])
        : new ReportViewSection("Evaluaciones IBS-SSS",
            data.Assessments
                .OrderBy(a => a.Date)
                .Select(a => Bullet($"{Date(a.Date)}: {a.Score}/500 ({ReportLabels.IbsSssCategory(a.Category)})"))
                .ToList(),
            ShowsIbsSssChart: true);

    private static ReportViewSection Recommendations(ClinicalReportData data)
    {
        if (data.ApprovedRecommendations.Count == 0)
        {
            return new ReportViewSection("Recomendaciones aprobadas", [Text("Sin recomendaciones aprobadas en el período.")]);
        }

        var lines = new List<ReportViewLine>();
        foreach (var recommendation in data.ApprovedRecommendations.OrderBy(r => r.ApprovedAt))
        {
            var approvedOn = $"aprobada el {Date(ReportClock.LimaDate(recommendation.ApprovedAt))}";
            var heading = recommendation.IsManual
                ? $"{recommendation.Title ?? "Recomendación del nutricionista"} · {approvedOn}"
                : $"Recomendación del sistema · {approvedOn} · {FoodCount(recommendation.Foods.Count)}";
            lines.Add(new ReportViewLine(heading, ReportLineKind.Subheading));

            if (recommendation.IsManual && !string.IsNullOrWhiteSpace(recommendation.Description))
            {
                lines.Add(new ReportViewLine(recommendation.Description, ReportLineKind.Indented));
            }

            if (recommendation.IsManual && recommendation.Steps.Count > 0)
            {
                lines.Add(new ReportViewLine("Pasos:", ReportLineKind.Indented));
                lines.AddRange(recommendation.Steps.Select((step, index) =>
                    new ReportViewLine($"{index + 1}. {step}", ReportLineKind.Indented)));
            }

            lines.AddRange(recommendation.Foods
                .GroupBy(food => food.ActionType)
                .OrderBy(group => ReportLabels.ActionOrder(group.Key))
                .Select(group => new ReportViewLine(
                    $"{ReportLabels.Action(group.Key)}: " + string.Join(", ", group.Select(food =>
                        food.SubstituteName is null ? food.FoodName : $"{food.FoodName} por {food.SubstituteName}")),
                    ReportLineKind.Indented)));

            if (!string.IsNullOrWhiteSpace(recommendation.ClinicalNote))
            {
                lines.Add(new ReportViewLine($"Nota clínica: {recommendation.ClinicalNote}", ReportLineKind.Note));
            }
        }

        return new ReportViewSection("Recomendaciones aprobadas", lines);
    }

    private static ReportViewSection Feedback(ClinicalReportData data) => new("Retroalimentación",
        data.Feedback.Count == 0
            ? [Text("Sin retroalimentación en el período.")]
            : data.Feedback.Select(f => Bullet(
                $"{Date(ReportClock.LimaDate(f.SubmittedAt))}: {(f.WasApplied ? "aplicada" : "no aplicada")} · " +
                ReportLabels.FeedbackOutcome(f.Outcome))).ToList());

    private static string FoodCount(int count) => count == 1 ? "1 alimento" : $"{count} alimentos";

    private static string Date(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static string DateTime(DateTime utc) => ReportClock.ToLima(utc).ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    private static ReportViewLine Text(string text) => new(text, ReportLineKind.Text);

    private static ReportViewLine Bullet(string text) => new(text, ReportLineKind.Bullet);
}
