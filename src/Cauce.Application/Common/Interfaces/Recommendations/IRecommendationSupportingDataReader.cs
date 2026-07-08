namespace Cauce.Application.Common.Interfaces.Recommendations;

/// <summary>
/// Lee las cifras de respaldo clínico que acompañan al detalle de una recomendación (US15 CA03,
/// bloque 4): actividad reciente del paciente en la ventana de análisis.
/// </summary>
public interface IRecommendationSupportingDataReader
{
    /// <summary>
    /// Obtiene las cifras de respaldo del paciente en la ventana indicada.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="fromUtc">Inicio de la ventana de análisis (inclusive), en UTC.</param>
    /// <param name="toUtc">Fin de la ventana de análisis (exclusive), en UTC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las cifras de respaldo.</returns>
    Task<RecommendationSupportingDataSnapshot> GetAsync(
        Guid patientId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);
}

/// <summary>
/// Cifras de respaldo del paciente en una ventana de análisis.
/// </summary>
/// <param name="SymptomCount">Cantidad de síntomas registrados en la ventana.</param>
/// <param name="MealCount">Cantidad de comidas registradas en la ventana.</param>
/// <param name="TopHighFodmapFoods">Hasta tres nombres de alimentos FODMAP alto más consumidos en la ventana.</param>
public sealed record RecommendationSupportingDataSnapshot(
    int SymptomCount,
    int MealCount,
    IReadOnlyList<string> TopHighFodmapFoods);
