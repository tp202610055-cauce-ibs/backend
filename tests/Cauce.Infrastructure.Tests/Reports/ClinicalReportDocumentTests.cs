using Cauce.Application.Reports.Contracts;
using Cauce.Infrastructure.Reports;
using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Cauce.Infrastructure.Tests.Reports;

/// <summary>
/// Pruebas de composición del documento PDF del reporte clínico.
///
/// <para>No se afirma sobre el texto del PDF: QuestPDF embebe subconjuntos de fuente y el contenido
/// queda codificado con la tabla de glifos del subconjunto, así que leerlo de vuelta no es confiable.
/// Lo que sí se comprueba es que cada sección nueva emita contenido: un reporte con historial de
/// comidas y con detalle de síntomas pesa estrictamente más que el mismo reporte sin ellos, y eso solo
/// puede pasar si las secciones se están dibujando.</para>
/// </summary>
public sealed class ClinicalReportDocumentTests
{
    static ClinicalReportDocumentTests()
    {
        // En producción la licencia la fija el constructor estático de PdfReportGenerator, que es el
        // único que construye este documento. Aquí se compone el documento directamente, así que hay
        // que declararla igual.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly DateOnly PeriodStart = new(2026, 6, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 8, 30);

    private static ClinicalReportData Data(
        IReadOnlyList<ReportMealEntry>? meals = null,
        IReadOnlyList<ReportSymptomSummary>? symptoms = null,
        IReadOnlyList<ReportSymptomEntry>? symptomEntries = null) => new(
        PatientCode: "PAC-0042",
        PatientInitials: "R. G. C.",
        PatientAge: 34,
        IbsSubtype: "IbsM",
        WeightKg: 62m,
        HeightCm: 165m,
        Medications: null,
        NutritionistName: null,
        PeriodStart: PeriodStart,
        PeriodEnd: PeriodEnd,
        GeneratedAt: new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc),
        Allergies: [],
        MealCount: meals?.Count ?? 0,
        Meals: meals ?? [],
        FrequentFoods: [],
        Symptoms: symptoms ?? [],
        SymptomEntries: symptomEntries ?? [],
        Assessments: [],
        ApprovedRecommendations: [],
        Feedback: []);

    private static byte[] Render(ClinicalReportData data) =>
        new ClinicalReportDocument(data, ibsSssChartPng: null).GeneratePdf();

    [Fact]
    public void Compose_EmptyReport_ProducesAValidPdf()
    {
        var bytes = Render(Data());

        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void Compose_WithMealHistory_EmitsMoreContentThanWithout()
    {
        var meals = Enumerable.Range(1, 12)
            .Select(day => new ReportMealEntry(
                new DateTime(2026, 8, day, 13, 0, 0, DateTimeKind.Utc),
                "Lunch",
                ["Arroz blanco cocido", "Pollo a la plancha", "Zanahoria cocida"]))
            .ToList();

        Render(Data(meals: meals)).Length.Should().BeGreaterThan(Render(Data()).Length);
    }

    [Fact]
    public void Compose_WithSymptomIntensities_EmitsMoreContentThanWithout()
    {
        var symptoms = new List<ReportSymptomSummary>
        {
            new("AbdominalPain", 6, 62.5m, 20, 90),
            new("Bloating", 3, 41m, 30, 55)
        };
        var entries = Enumerable.Range(1, 9)
            .Select(day => new ReportSymptomEntry(
                new DateTime(2026, 8, day, 20, 0, 0, DateTimeKind.Utc), "AbdominalPain", 40 + day, day % 2 == 0))
            .ToList();

        Render(Data(symptoms: symptoms, symptomEntries: entries)).Length
            .Should().BeGreaterThan(Render(Data()).Length);
    }

    [Fact]
    public void Compose_TruncatedMealHistory_StillRenders()
    {
        // MealCount mayor que las comidas listadas: el documento avisa del recorte en vez de mentir
        // sobre cuántas hubo.
        var meals = new List<ReportMealEntry>
        {
            new(new DateTime(2026, 8, 1, 13, 0, 0, DateTimeKind.Utc), "Lunch", ["Quinua blanca cocida"])
        };
        var data = Data(meals: meals) with { MealCount = 500 };

        var bytes = Render(data);

        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
        bytes.Length.Should().BeGreaterThan(Render(Data()).Length);
    }
}
