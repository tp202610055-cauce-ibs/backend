namespace Cauce.Domain.ClinicalRegistry.Enums;

/// <summary>
/// Categoría de severidad derivada del puntaje total IBS-SSS (0–500). En base de
/// datos se persiste como <c>varchar</c> en snake_case lowercase (<c>mild</c>,
/// <c>moderate</c>, <c>severe</c>).
/// </summary>
public enum SeverityCategory
{
    /// <summary>
    /// Severidad leve, puntaje total en el rango [0, 174].
    /// </summary>
    Mild = 0,

    /// <summary>
    /// Severidad moderada, puntaje total en el rango [175, 300].
    /// </summary>
    Moderate = 1,

    /// <summary>
    /// Severidad alta, puntaje total en el rango [301, 500].
    /// </summary>
    Severe = 2
}
