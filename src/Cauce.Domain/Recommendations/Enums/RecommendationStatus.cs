namespace Cauce.Domain.Recommendations.Enums;

/// <summary>
/// Estado de una recomendación dentro del flujo Human-In-The-Loop. En base de datos se
/// persiste como <c>varchar</c> en snake_case (por ejemplo, <c>"pending_review"</c>).
/// </summary>
public enum RecommendationStatus
{
    /// <summary>
    /// Recién generada por el motor; aún no clasificada para revisión o entrega.
    /// </summary>
    Generated,

    /// <summary>
    /// En espera de revisión por un nutricionista.
    /// </summary>
    PendingReview,

    /// <summary>
    /// Aprobada (por un nutricionista o por auto-aprobación), lista para entregar.
    /// </summary>
    Approved,

    /// <summary>
    /// Rechazada por un nutricionista. Estado final.
    /// </summary>
    Rejected,

    /// <summary>
    /// Entregada al paciente en la aplicación móvil.
    /// </summary>
    Delivered,

    /// <summary>
    /// El paciente envió su retroalimentación clínica. Estado final.
    /// </summary>
    FeedbackReceived,

    /// <summary>
    /// Expirada por superar su ventana de vigencia sin ser entregada. Estado final.
    /// </summary>
    Expired
}
