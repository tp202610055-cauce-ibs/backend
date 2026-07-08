namespace Cauce.Domain.ClinicalRegistry.Enums;

/// <summary>
/// Categoría de un término del glosario clínico (US27). En base de datos se persiste como
/// <c>varchar</c> en snake_case lowercase (<c>nutritional</c>, <c>clinical_ibs</c>, <c>system</c>).
/// </summary>
public enum GlossaryCategory
{
    /// <summary>
    /// Término nutricional (por ejemplo, FODMAP, lactosa, fibra dietética).
    /// </summary>
    Nutritional = 0,

    /// <summary>
    /// Término clínico del SII (por ejemplo, IBS-SSS, Roma IV, distensión abdominal).
    /// </summary>
    ClinicalIbs = 1,

    /// <summary>
    /// Término del sistema o del proceso (por ejemplo, recomendación, revisión HITL, consentimiento).
    /// </summary>
    System = 2
}
