namespace Cauce.Domain.Recommendations.Enums;

/// <summary>
/// Acción dietética sugerida por una recomendación sobre un alimento. En base de datos se
/// persiste como <c>varchar</c> en snake_case (por ejemplo, <c>"substitute"</c>).
/// </summary>
public enum ActionType
{
    /// <summary>
    /// Sugerir incorporar el alimento (carga FODMAP baja).
    /// </summary>
    Suggest,

    /// <summary>
    /// Reducir el consumo del alimento (carga FODMAP moderada).
    /// </summary>
    Reduce,

    /// <summary>
    /// Evitar el alimento (carga FODMAP alta).
    /// </summary>
    Avoid,

    /// <summary>
    /// Sustituir el alimento por otro de la misma categoría con menor riesgo.
    /// </summary>
    Substitute
}
