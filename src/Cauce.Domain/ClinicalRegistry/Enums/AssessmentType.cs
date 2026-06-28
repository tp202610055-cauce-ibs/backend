namespace Cauce.Domain.ClinicalRegistry.Enums;

/// <summary>
/// Tipo de evaluación IBS-SSS. En base de datos se persiste como <c>varchar</c> en
/// snake_case lowercase (<c>baseline</c>, <c>periodic</c>).
/// </summary>
public enum AssessmentType
{
    /// <summary>
    /// Evaluación de línea base, aplicada una sola vez al inicio del seguimiento.
    /// </summary>
    Baseline = 0,

    /// <summary>
    /// Evaluación periódica, aplicada cada 14 días durante el seguimiento.
    /// </summary>
    Periodic = 1
}
