namespace Cauce.Domain.Patients.Enums;

/// <summary>
/// Severidad declarada de una alergia o intolerancia del paciente. En base de datos
/// se persiste como <c>varchar</c> en snake_case (por ejemplo, <c>"moderate"</c>).
/// </summary>
public enum AllergySeverity
{
    /// <summary>
    /// Leve.
    /// </summary>
    Mild = 0,

    /// <summary>
    /// Moderada.
    /// </summary>
    Moderate = 1,

    /// <summary>
    /// Severa.
    /// </summary>
    Severe = 2
}
