namespace Cauce.Domain.Recommendations.Enums;

/// <summary>
/// Motivo por el que una recomendación se archiva (deja de estar activa para el paciente). En base de
/// datos se persiste como <c>varchar</c> en snake_case (por ejemplo, <c>"temporal_expiration"</c>).
/// </summary>
public enum ArchiveReason
{
    /// <summary>
    /// Archivada automáticamente por superar su fecha de vigencia (<c>ValidUntil</c>).
    /// </summary>
    TemporalExpiration,

    /// <summary>
    /// Archivada porque el nutricionista la sustituyó manualmente por otra.
    /// </summary>
    ManualSubstitution,

    /// <summary>
    /// Archivada porque se alcanzó el objetivo clínico que la motivó.
    /// </summary>
    ObjectiveMet,

    /// <summary>
    /// Archivada por un cambio en el plan de tratamiento del paciente.
    /// </summary>
    PlanChange
}
