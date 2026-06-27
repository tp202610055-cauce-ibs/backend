namespace Cauce.Domain.Patients.Enums;

/// <summary>
/// Sexo biológico del paciente. En base de datos se persiste como <c>varchar</c>
/// en snake_case lowercase (por ejemplo, <c>"female"</c>).
/// </summary>
public enum BiologicalSex
{
    /// <summary>
    /// Masculino.
    /// </summary>
    Male = 0,

    /// <summary>
    /// Femenino.
    /// </summary>
    Female = 1,

    /// <summary>
    /// Otro.
    /// </summary>
    Other = 2
}
