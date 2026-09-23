namespace Cauce.Application.Reports.Contracts;

/// <summary>
/// Datos consolidados de un reporte clínico, que consume el generador de PDF. No contiene datos
/// técnicos del sistema (identificadores de Keycloak, correos crudos, IPs): solo información
/// clínica y de identificación mínima del paciente.
/// </summary>
/// <param name="PatientCode">Código correlativo legible del paciente (<c>PAC-0042</c>).</param>
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
/// <param name="Meals">Historial cronológico de comidas del período (HU0024 CA01).</param>
/// <param name="FrequentFoods">Alimentos más frecuentes.</param>
/// <param name="Symptoms">Resumen de síntomas por tipo, con sus intensidades.</param>
/// <param name="SymptomEntries">Historial cronológico de síntomas del período con su intensidad (HU0024 CA01).</param>
/// <param name="Assessments">Evaluaciones IBS-SSS del período.</param>
/// <param name="ApprovedRecommendations">Recomendaciones aprobadas del período.</param>
/// <param name="Feedback">Retroalimentación recibida.</param>
public sealed record ClinicalReportData(
    string PatientCode,
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
    IReadOnlyList<ReportMealEntry> Meals,
    IReadOnlyList<ReportFoodFrequency> FrequentFoods,
    IReadOnlyList<ReportSymptomSummary> Symptoms,
    IReadOnlyList<ReportSymptomEntry> SymptomEntries,
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

/// <summary>Resumen de un tipo de síntoma, con la distribución de intensidad observada.</summary>
/// <param name="SymptomType">Tipo de síntoma.</param>
/// <param name="Count">Cantidad de ocurrencias.</param>
/// <param name="AverageIntensity">Intensidad media (1–100) de las ocurrencias del período.</param>
/// <param name="MinIntensity">Intensidad mínima registrada.</param>
/// <param name="MaxIntensity">Intensidad máxima registrada.</param>
public sealed record ReportSymptomSummary(
    string SymptomType,
    int Count,
    decimal AverageIntensity,
    int MinIntensity,
    int MaxIntensity);

/// <summary>Comida individual del historial del reporte.</summary>
/// <param name="ConsumedAt">Momento de consumo, en UTC.</param>
/// <param name="MealTime">Momento del día (desayuno, almuerzo, cena, colación).</param>
/// <param name="Items">Nombres de los alimentos que la componen.</param>
public sealed record ReportMealEntry(DateTime ConsumedAt, string MealTime, IReadOnlyList<string> Items);

/// <summary>Síntoma individual del historial del reporte.</summary>
/// <param name="OccurredAt">Momento de ocurrencia, en UTC.</param>
/// <param name="SymptomType">Tipo de síntoma.</param>
/// <param name="Intensity">Intensidad reportada (1–100).</param>
/// <param name="AssociatedWithMeal">Indica si quedó correlacionado con una comida en la ventana de 4 h.</param>
public sealed record ReportSymptomEntry(
    DateTime OccurredAt,
    string SymptomType,
    int Intensity,
    bool AssociatedWithMeal);

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
