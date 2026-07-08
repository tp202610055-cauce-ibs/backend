namespace Cauce.Application.Reports.Contracts;

/// <summary>
/// Datos consolidados de un reporte clínico, que consume el generador de PDF. No contiene datos
/// técnicos del sistema (identificadores de Keycloak, correos crudos, IPs): solo información
/// clínica y de identificación mínima del paciente.
/// </summary>
/// <param name="PatientInitials">Iniciales del paciente (por ejemplo, "R. G. C.").</param>
/// <param name="PatientAge">Edad del paciente.</param>
/// <param name="IbsSubtype">Subtipo de SII.</param>
/// <param name="WeightKg">Peso en kilogramos.</param>
/// <param name="HeightCm">Estatura en centímetros.</param>
/// <param name="Medications">Medicación declarada, o <see langword="null"/>.</param>
/// <param name="NutritionistName">Nombre del nutricionista, o <see langword="null"/> si el paciente no tiene uno asignado (autoreporte US24).</param>
/// <param name="PeriodStart">Inicio del período.</param>
/// <param name="PeriodEnd">Fin del período.</param>
/// <param name="GeneratedAt">Momento de generación, en UTC.</param>
/// <param name="Allergies">Alergias declaradas.</param>
/// <param name="MealCount">Cantidad de comidas registradas en el período.</param>
/// <param name="FrequentFoods">Alimentos más frecuentes.</param>
/// <param name="Symptoms">Resumen de síntomas por tipo.</param>
/// <param name="Assessments">Evaluaciones IBS-SSS del período.</param>
/// <param name="ApprovedRecommendations">Recomendaciones aprobadas del período.</param>
/// <param name="Feedback">Retroalimentación recibida.</param>
public sealed record ClinicalReportData(
    string PatientInitials,
    int PatientAge,
    string IbsSubtype,
    decimal WeightKg,
    decimal HeightCm,
    string? Medications,
    string? NutritionistName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateTime GeneratedAt,
    IReadOnlyList<ReportAllergy> Allergies,
    int MealCount,
    IReadOnlyList<ReportFoodFrequency> FrequentFoods,
    IReadOnlyList<ReportSymptomSummary> Symptoms,
    IReadOnlyList<ReportAssessment> Assessments,
    IReadOnlyList<ReportRecommendation> ApprovedRecommendations,
    IReadOnlyList<ReportFeedback> Feedback);

/// <summary>Alergia declarada para el reporte.</summary>
/// <param name="Name">Nombre de la alergia.</param>
/// <param name="Severity">Severidad declarada.</param>
public sealed record ReportAllergy(string Name, string Severity);

/// <summary>Frecuencia de consumo de un alimento.</summary>
/// <param name="FoodName">Nombre del alimento.</param>
/// <param name="Count">Cantidad de veces consumido.</param>
public sealed record ReportFoodFrequency(string FoodName, int Count);

/// <summary>Resumen de un tipo de síntoma.</summary>
/// <param name="SymptomType">Tipo de síntoma.</param>
/// <param name="Count">Cantidad de ocurrencias.</param>
public sealed record ReportSymptomSummary(string SymptomType, int Count);

/// <summary>Evaluación IBS-SSS del reporte.</summary>
/// <param name="Date">Fecha de la evaluación.</param>
/// <param name="Score">Puntaje total.</param>
/// <param name="Category">Categoría de severidad.</param>
public sealed record ReportAssessment(DateOnly Date, int Score, string Category);

/// <summary>Recomendación aprobada del reporte.</summary>
/// <param name="ApprovedAt">Momento de aprobación.</param>
/// <param name="ItemsCount">Cantidad de ítems.</param>
/// <param name="ExplanationText">Explicación, o <see langword="null"/>.</param>
public sealed record ReportRecommendation(DateTime ApprovedAt, int ItemsCount, string? ExplanationText);

/// <summary>Retroalimentación del reporte.</summary>
/// <param name="SubmittedAt">Momento de envío.</param>
/// <param name="WasApplied">Si se aplicó la recomendación.</param>
/// <param name="Outcome">Resultado percibido.</param>
public sealed record ReportFeedback(DateTime SubmittedAt, bool WasApplied, string Outcome);
