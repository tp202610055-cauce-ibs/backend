namespace Cauce.Domain.Patients.Enums;

/// <summary>
/// Subtipo clínico de Síndrome de Intestino Irritable según los criterios Roma IV.
/// En base de datos se persiste como <c>varchar</c> en snake_case (por ejemplo,
/// <c>"ibs_d"</c>).
/// </summary>
public enum IbsSubtype
{
    /// <summary>
    /// SII con predominio de diarrea (IBS-D).
    /// </summary>
    IbsD = 0,

    /// <summary>
    /// SII con predominio de estreñimiento (IBS-C).
    /// </summary>
    IbsC = 1,

    /// <summary>
    /// SII de hábito intestinal mixto (IBS-M).
    /// </summary>
    IbsM = 2,

    /// <summary>
    /// SII no clasificable (IBS-U).
    /// </summary>
    IbsU = 3
}
