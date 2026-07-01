namespace Cauce.Domain.Recommendations.Enums;

/// <summary>
/// Resultado clínico declarado por el paciente tras aplicar una recomendación. En base de
/// datos se persiste como <c>varchar</c> en snake_case (por ejemplo, <c>"no_change"</c>).
/// </summary>
public enum FeedbackOutcome
{
    /// <summary>
    /// El paciente percibió una mejoría de sus síntomas.
    /// </summary>
    Improvement,

    /// <summary>
    /// El paciente no percibió cambios en sus síntomas.
    /// </summary>
    NoChange,

    /// <summary>
    /// El paciente percibió un empeoramiento de sus síntomas.
    /// </summary>
    Worsening
}
