namespace Cauce.Domain.Patients.Enums;

/// <summary>
/// Naturaleza de la reacción adversa a un alimento. En base de datos se persiste
/// como <c>varchar</c> en snake_case (por ejemplo, <c>"intolerance"</c>).
/// </summary>
public enum AllergyType
{
    /// <summary>
    /// Reacción inmunológica (alergia propiamente dicha).
    /// </summary>
    Allergy = 0,

    /// <summary>
    /// Intolerancia metabólica o digestiva.
    /// </summary>
    Intolerance = 1,

    /// <summary>
    /// Sensibilidad o reacción adversa no inmunológica.
    /// </summary>
    Sensitivity = 2
}
