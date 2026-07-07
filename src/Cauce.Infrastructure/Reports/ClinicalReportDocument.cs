using Cauce.Application.Reports.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Documento QuestPDF del reporte clínico del paciente. Presenta, sin datos técnicos del sistema, el
/// resumen clínico del período: perfil, alergias, adherencia al registro, síntomas, evaluaciones
/// IBS-SSS, recomendaciones aprobadas y retroalimentación.
/// </summary>
public sealed class ClinicalReportDocument : IDocument
{
    private readonly ClinicalReportData _data;
    private readonly byte[]? _ibsSssChartPng;

    /// <summary>
    /// Inicializa el documento con los datos consolidados del reporte y, opcionalmente, el gráfico de
    /// evolución IBS-SSS ya renderizado como PNG (TS11/US22 CA01).
    /// </summary>
    /// <param name="data">Datos del reporte.</param>
    /// <param name="ibsSssChartPng">PNG del gráfico de evolución IBS-SSS, o <see langword="null"/> si no aplica.</param>
    public ClinicalReportDocument(ClinicalReportData data, byte[]? ibsSssChartPng = null)
    {
        _data = data;
        _ibsSssChartPng = ibsSssChartPng;
    }

    /// <inheritdoc />
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    /// <inheritdoc />
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(40);
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(style => style.FontSize(10).FontColor(Colors.Grey.Darken4));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Reporte clínico — Cauce").FontSize(18).Bold().FontColor(Colors.Teal.Darken2);
            column.Item().Text($"Paciente: {_data.PatientInitials}   ·   Edad: {_data.PatientAge} años   ·   SII: {_data.IbsSubtype}");
            column.Item().Text(
                $"Período: {_data.PeriodStart:yyyy-MM-dd} a {_data.PeriodEnd:yyyy-MM-dd}   ·   " +
                $"Generado: {_data.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
            if (!string.IsNullOrWhiteSpace(_data.NutritionistName))
            {
                column.Item().Text($"Nutricionista: {_data.NutritionistName}");
            }

            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(12);

            column.Item().Element(c => Section(c, "Perfil clínico", inner =>
            {
                inner.Item().Text($"Peso: {_data.WeightKg:0.#} kg   ·   Estatura: {_data.HeightCm:0.#} cm");
                inner.Item().Text(string.IsNullOrWhiteSpace(_data.Medications)
                    ? "Medicación: no declarada"
                    : $"Medicación: {_data.Medications}");
            }));

            column.Item().Element(c => Section(c, "Alergias declaradas", inner =>
            {
                if (_data.Allergies.Count == 0)
                {
                    inner.Item().Text("Sin alergias declaradas.");
                    return;
                }

                foreach (var allergy in _data.Allergies)
                {
                    inner.Item().Text($"• {allergy.Name} ({allergy.Severity})");
                }
            }));

            column.Item().Element(c => Section(c, "Adherencia al registro", inner =>
            {
                inner.Item().Text($"Comidas registradas: {_data.MealCount}");
                if (_data.FrequentFoods.Count > 0)
                {
                    inner.Item().PaddingTop(4).Text("Alimentos más frecuentes:").SemiBold();
                    foreach (var food in _data.FrequentFoods)
                    {
                        inner.Item().Text($"• {food.FoodName}: {food.Count}");
                    }
                }
            }));

            column.Item().Element(c => Section(c, "Síntomas", inner =>
            {
                if (_data.Symptoms.Count == 0)
                {
                    inner.Item().Text("Sin síntomas registrados en el período.");
                    return;
                }

                foreach (var symptom in _data.Symptoms)
                {
                    inner.Item().Text($"• {symptom.SymptomType}: {symptom.Count}");
                }
            }));

            column.Item().Element(c => Section(c, "Evaluaciones IBS-SSS", inner =>
            {
                if (_data.Assessments.Count == 0)
                {
                    inner.Item().Text("Sin evaluaciones en el período.");
                    return;
                }

                foreach (var assessment in _data.Assessments)
                {
                    inner.Item().Text($"• {assessment.Date:yyyy-MM-dd}: {assessment.Score}/500 ({assessment.Category})");
                }

                if (_ibsSssChartPng is not null)
                {
                    inner.Item().PaddingTop(8).Image(_ibsSssChartPng).FitWidth();
                }
            }));

            column.Item().Element(c => Section(c, "Recomendaciones aprobadas", inner =>
            {
                if (_data.ApprovedRecommendations.Count == 0)
                {
                    inner.Item().Text("Sin recomendaciones aprobadas en el período.");
                    return;
                }

                foreach (var recommendation in _data.ApprovedRecommendations)
                {
                    inner.Item().Text($"• {recommendation.ApprovedAt:yyyy-MM-dd}: {recommendation.ItemsCount} ítems");
                    if (!string.IsNullOrWhiteSpace(recommendation.ExplanationText))
                    {
                        inner.Item().PaddingLeft(10).Text(recommendation.ExplanationText).FontColor(Colors.Grey.Darken2).Italic();
                    }
                }
            }));

            column.Item().Element(c => Section(c, "Retroalimentación", inner =>
            {
                if (_data.Feedback.Count == 0)
                {
                    inner.Item().Text("Sin retroalimentación en el período.");
                    return;
                }

                foreach (var feedback in _data.Feedback)
                {
                    var applied = feedback.WasApplied ? "aplicada" : "no aplicada";
                    inner.Item().Text($"• {feedback.SubmittedAt:yyyy-MM-dd}: {applied} — {feedback.Outcome}");
                }
            }));
        });
    }

    private static void Section(IContainer container, string title, Action<ColumnDescriptor> content)
    {
        container.Column(column =>
        {
            column.Item().Text(title).FontSize(12).Bold().FontColor(Colors.Teal.Darken1);
            column.Item().PaddingTop(2).Column(content);
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Documento confidencial — Ley N.° 29733. No redistribuir.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                row.ConstantItem(120).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1));
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });
    }
}
