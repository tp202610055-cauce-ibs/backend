namespace Cauce.Application.Recommendations.Dtos;

/// <summary>
/// Bloque 4 del detalle de una recomendación (US15 CA03): datos de respaldo clínico calculados por
/// consultas deterministas sobre la actividad del paciente en la ventana de análisis.
/// </summary>
/// <param name="SymptomCountsLast14d">Cantidad total de síntomas registrados en los últimos 14 días.</param>
/// <param name="MealCountsLast14d">Cantidad total de comidas registradas en los últimos 14 días.</param>
/// <param name="TopFodmapHighFoodsLast14d">Hasta tres alimentos FODMAP alto más consumidos en los últimos 14 días.</param>
/// <param name="CorrelationWindowHours">Ventana de correlación síntoma-comida, en horas (constante: 4).</param>
/// <param name="AnalysisWindowFrom">Inicio de la ventana de análisis, en UTC.</param>
/// <param name="AnalysisWindowTo">Fin de la ventana de análisis, en UTC.</param>
public sealed record RecommendationSupportingDataDto(
    int SymptomCountsLast14d,
    int MealCountsLast14d,
    IReadOnlyList<string> TopFodmapHighFoodsLast14d,
    int CorrelationWindowHours,
    DateTime AnalysisWindowFrom,
    DateTime AnalysisWindowTo);
