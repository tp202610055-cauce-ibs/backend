using Cauce.Application.Reports.Contracts;
using Cauce.Infrastructure.Reports;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Reports;

/// <summary>
/// Pruebas del contenido del reporte antes de dibujarlo: textos en español, horas de Lima,
/// recomendaciones aprobadas con sus alimentos o su texto manual y frecuencia de registro.
/// </summary>
public sealed class ClinicalReportViewBuilderTests
{
    private static ClinicalReportData Data(
        IReadOnlyList<ReportAssessment>? assessments = null,
        IReadOnlyList<ReportRecommendation>? recommendations = null,
        IReadOnlyList<ReportMealEntry>? meals = null,
        IReadOnlyList<ReportSymptomEntry>? symptomEntries = null) => new(
        PatientCode: "PAC-0042",
        PatientInitials: "R. G. C.",
        PatientAge: 34,
        IbsSubtype: "IbsD",
        WeightKg: 62m,
        HeightCm: 165m,
        Medications: null,
        NutritionistName: "Ana Gómez",
        PeriodStart: new DateOnly(2026, 8, 1),
        PeriodEnd: new DateOnly(2026, 8, 31),
        GeneratedAt: new DateTime(2026, 9, 1, 2, 30, 0, DateTimeKind.Utc),
        Allergies: [new ReportAllergy("Lactosa", "Moderate")],
        MealCount: meals?.Count ?? 0,
        Meals: meals ?? [],
        FrequentFoods: [],
        Symptoms: [new ReportSymptomSummary("AbdominalPain", 1, 65m, 65, 65)],
        SymptomEntries: symptomEntries ?? [],
        Assessments: assessments ?? [],
        ApprovedRecommendations: recommendations ?? [],
        Feedback: [],
        DaysWithRecords: 20,
        PeriodDays: 31);

    private static IReadOnlyList<string> AllText(ClinicalReportView view) =>
        view.Header.Concat(view.Sections.SelectMany(s => s.Lines.Select(l => l.Text).Prepend(s.Title))).ToList();

    private static ReportViewSection Section(ClinicalReportView view, string title) =>
        view.Sections.Single(section => section.Title == title);

    [Fact]
    public void Build_GeneratedAt_IsShownInLimaTime()
    {
        var view = ClinicalReportViewBuilder.Build(Data());

        view.Header.Should().Contain(line => line.Contains("31/08/2026 21:30 (hora de Lima)"));
        AllText(view).Should().NotContain(line => line.Contains("UTC"));
    }

    [Fact]
    public void Build_PeriodAndProfile_AreInPeruvianSpanish()
    {
        var view = ClinicalReportViewBuilder.Build(Data());

        view.Header.Should().Contain(line => line.Contains("Período: 01/08/2026 al 31/08/2026"));
        view.Header.Should().Contain(line => line.Contains("SII-D (diarrea)"));
        Section(view, "Alergias declaradas").Lines.Should().Contain(line => line.Text == "Lactosa (severidad moderada)");
        Section(view, "Síntomas").Lines.Should().Contain(line => line.Text.StartsWith("Dolor abdominal: 1 episodio"));
    }

    [Fact]
    public void Build_NoEnglishEnumNameLeaksIntoTheText()
    {
        var meals = new[] { new ReportMealEntry(new DateTime(2026, 8, 5, 18, 0, 0, DateTimeKind.Utc), "Lunch", ["Causa rellena"]) };
        var symptoms = new[] { new ReportSymptomEntry(new DateTime(2026, 8, 5, 20, 0, 0, DateTimeKind.Utc), "Bloating", 40, false) };
        var assessments = new[] { new ReportAssessment(new DateOnly(2026, 8, 5), 320, "Severe") };

        var text = AllText(ClinicalReportViewBuilder.Build(Data(assessments, meals: meals, symptomEntries: symptoms)));

        foreach (var englishName in new[] { "Lunch", "Bloating", "Severe", "AbdominalPain", "IbsD", "Moderate" })
        {
            text.Should().NotContain(line => line.Contains(englishName), $"«{englishName}» debe salir en español");
        }
    }

    [Fact]
    public void Build_MealAndSymptomTimes_AreShownInLimaTime()
    {
        var meals = new[] { new ReportMealEntry(new DateTime(2026, 8, 6, 3, 0, 0, DateTimeKind.Utc), "Dinner", ["Causa rellena"]) };
        var symptoms = new[] { new ReportSymptomEntry(new DateTime(2026, 8, 6, 4, 15, 0, DateTimeKind.Utc), "AbdominalPain", 65, true) };

        var view = ClinicalReportViewBuilder.Build(Data(meals: meals, symptomEntries: symptoms));

        Section(view, "Historial de comidas").Lines.Should().Contain(line => line.Text == "05/08/2026 22:00 · Cena: Causa rellena");
        Section(view, "Síntomas").Lines.Should().Contain(line => line.Text.StartsWith("05/08/2026 23:15 · Dolor abdominal, intensidad 65/100"));
    }

    [Fact]
    public void Build_IbsSssScores_AreChronologicalWithTheirDate()
    {
        var assessments = new[]
        {
            new ReportAssessment(new DateOnly(2026, 8, 19), 280, "Moderate"),
            new ReportAssessment(new DateOnly(2026, 8, 5), 320, "Severe")
        };

        var lines = Section(ClinicalReportViewBuilder.Build(Data(assessments)), "Evaluaciones IBS-SSS").Lines;

        lines.Select(line => line.Text).Should().Equal("05/08/2026: 320/500 (severo)", "19/08/2026: 280/500 (moderado)");
    }

    [Fact]
    public void Build_RecordFrequency_CountsDaysOverThePeriod()
    {
        var lines = Section(ClinicalReportViewBuilder.Build(Data()), "Frecuencia de registro").Lines;

        lines.Should().Contain(line => line.Text == "20 días con registros de 31");
    }

    [Fact]
    public void Build_SystemRecommendation_ListsFoodsByActionAndTheClinicalNote()
    {
        var recommendation = new ReportRecommendation(
            ApprovedAt: new DateTime(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc),
            IsManual: false,
            Title: null,
            Description: null,
            Steps: [],
            Foods:
            [
                new ReportRecommendationFood("Reduce", "Cebolla cocida", null),
                new ReportRecommendationFood("Reduce", "Pan de trigo blanco", null),
                new ReportRecommendationFood("Substitute", "Leche entera de vaca", "Leche sin lactosa")
            ],
            ClinicalNote: "Reducir cebolla y pan de trigo por intolerancia a FODMAP");

        var texts = Section(ClinicalReportViewBuilder.Build(Data(recommendations: [recommendation])), "Recomendaciones aprobadas")
            .Lines.Select(line => line.Text).ToList();

        texts.Should().Contain("Recomendación del sistema · aprobada el 20/08/2026 · 3 alimentos");
        texts.Should().Contain("Reducir: Cebolla cocida, Pan de trigo blanco");
        texts.Should().Contain("Sustituir: Leche entera de vaca por Leche sin lactosa");
        texts.Should().Contain("Nota clínica: Reducir cebolla y pan de trigo por intolerancia a FODMAP");
    }

    [Fact]
    public void Build_ManualRecommendation_ShowsTitleDescriptionStepsAndNote()
    {
        var recommendation = new ReportRecommendation(
            ApprovedAt: new DateTime(2026, 8, 21, 2, 0, 0, DateTimeKind.Utc),
            IsManual: true,
            Title: "Hidratación entre comidas",
            Description: "Beber agua entre las comidas principales.",
            Steps: ["Tomar un vaso a media mañana.", "Tomar otro a media tarde."],
            Foods: [],
            ClinicalNote: "Indicada por estreñimiento leve.");

        var texts = Section(ClinicalReportViewBuilder.Build(Data(recommendations: [recommendation])), "Recomendaciones aprobadas")
            .Lines.Select(line => line.Text).ToList();

        texts.Should().ContainInOrder(
            "Hidratación entre comidas · aprobada el 20/08/2026",
            "Beber agua entre las comidas principales.",
            "Pasos:",
            "1. Tomar un vaso a media mañana.",
            "2. Tomar otro a media tarde.",
            "Nota clínica: Indicada por estreñimiento leve.");
    }
}
