using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Lógica pura de puntuación del cuestionario IBS-SSS (Irritable Bowel Syndrome —
/// Severity Scoring System). Centraliza la suma de las cinco dimensiones y la
/// categorización de severidad para que la entidad de dominio y los servicios de la
/// capa de aplicación compartan exactamente las mismas constantes y reglas.
/// </summary>
public static class IbsSssScoring
{
    /// <summary>
    /// Valor mínimo válido de cada dimensión individual.
    /// </summary>
    public const int DimensionMin = 0;

    /// <summary>
    /// Valor máximo válido de cada dimensión individual.
    /// </summary>
    public const int DimensionMax = 100;

    /// <summary>
    /// Cota superior (inclusive) de la categoría leve.
    /// </summary>
    public const int MildUpperBound = 174;

    /// <summary>
    /// Cota superior (inclusive) de la categoría moderada.
    /// </summary>
    public const int ModerateUpperBound = 300;

    /// <summary>
    /// Puntaje total mínimo posible.
    /// </summary>
    public const int TotalMin = 0;

    /// <summary>
    /// Puntaje total máximo posible.
    /// </summary>
    public const int TotalMax = 500;

    /// <summary>
    /// Diferencia mínima de puntaje que constituye una mejoría clínicamente significativa.
    /// </summary>
    public const int ClinicallySignificantDelta = 50;

    /// <summary>
    /// Suma las cinco dimensiones para obtener el puntaje total IBS-SSS (0–500).
    /// </summary>
    /// <param name="painSeverity">Severidad del dolor abdominal.</param>
    /// <param name="painFrequency">Frecuencia del dolor abdominal.</param>
    /// <param name="bloatingSeverity">Severidad de la distensión.</param>
    /// <param name="bowelHabitsDissatisfaction">Insatisfacción con el hábito intestinal.</param>
    /// <param name="lifeInterference">Interferencia con la vida diaria.</param>
    /// <returns>El puntaje total.</returns>
    public static int CalculateTotal(
        int painSeverity,
        int painFrequency,
        int bloatingSeverity,
        int bowelHabitsDissatisfaction,
        int lifeInterference)
    {
        return painSeverity + painFrequency + bloatingSeverity + bowelHabitsDissatisfaction + lifeInterference;
    }

    /// <summary>
    /// Clasifica un puntaje total en su categoría de severidad.
    /// </summary>
    /// <param name="totalScore">Puntaje total IBS-SSS.</param>
    /// <returns>La categoría de severidad correspondiente.</returns>
    public static SeverityCategory Categorize(int totalScore)
    {
        return totalScore switch
        {
            <= MildUpperBound => SeverityCategory.Mild,
            <= ModerateUpperBound => SeverityCategory.Moderate,
            _ => SeverityCategory.Severe
        };
    }
}
