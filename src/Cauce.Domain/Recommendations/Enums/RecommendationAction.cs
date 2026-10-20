namespace Cauce.Domain.Recommendations.Enums;

/// <summary>
/// Acción que el usuario que consulta una recomendación puede ejecutar sobre ella en su estado actual.
/// </summary>
public enum RecommendationAction
{
    /// <summary>
    /// Aprobar la recomendación pendiente de revisión.
    /// </summary>
    Approve,

    /// <summary>
    /// Modificar y aprobar la recomendación pendiente de revisión.
    /// </summary>
    Modify,

    /// <summary>
    /// Rechazar la recomendación pendiente de revisión.
    /// </summary>
    Reject,

    /// <summary>
    /// Archivar la recomendación activa en un estado terminal aprobado.
    /// </summary>
    Archive
}
