using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Calcula el puntaje total IBS-SSS y su categoría de severidad. La lógica subyacente
/// es compartida con la entidad de dominio mediante el helper de puntuación.
/// </summary>
public interface IIbsSssScorer
{
    /// <summary>
    /// Suma las cinco dimensiones para obtener el puntaje total (0–500).
    /// </summary>
    /// <param name="painSeverity">Severidad del dolor.</param>
    /// <param name="painFrequency">Frecuencia del dolor.</param>
    /// <param name="bloatingSeverity">Severidad de la distensión.</param>
    /// <param name="bowelHabitsDissatisfaction">Insatisfacción con el hábito intestinal.</param>
    /// <param name="lifeInterference">Interferencia con la vida diaria.</param>
    /// <returns>El puntaje total.</returns>
    int CalculateTotal(int painSeverity, int painFrequency, int bloatingSeverity, int bowelHabitsDissatisfaction, int lifeInterference);

    /// <summary>
    /// Clasifica un puntaje total en su categoría de severidad.
    /// </summary>
    /// <param name="totalScore">Puntaje total IBS-SSS.</param>
    /// <returns>La categoría de severidad.</returns>
    SeverityCategory Categorize(int totalScore);
}
