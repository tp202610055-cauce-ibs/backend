namespace Cauce.Domain.ClinicalRegistry.Enums;

/// <summary>
/// Tipo de síntoma gastrointestinal reportado por el paciente. En base de datos se
/// persiste como <c>varchar</c> en snake_case lowercase (por ejemplo,
/// <c>abdominal_pain</c>).
/// </summary>
public enum SymptomType
{
    /// <summary>
    /// Dolor abdominal.
    /// </summary>
    AbdominalPain = 0,

    /// <summary>
    /// Distensión o hinchazón abdominal.
    /// </summary>
    Bloating = 1,

    /// <summary>
    /// Flatulencia.
    /// </summary>
    Flatulence = 2,

    /// <summary>
    /// Diarrea.
    /// </summary>
    Diarrhea = 3,

    /// <summary>
    /// Estreñimiento.
    /// </summary>
    Constipation = 4,

    /// <summary>
    /// Otro síntoma no clasificado en las categorías anteriores.
    /// </summary>
    Other = 5
}
