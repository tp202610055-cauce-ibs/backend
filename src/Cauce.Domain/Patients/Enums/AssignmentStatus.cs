namespace Cauce.Domain.Patients.Enums;

/// <summary>
/// Estado de una asignación nutricionista-paciente. En base de datos se persiste
/// como <c>varchar</c> en snake_case (por ejemplo, <c>"active"</c>).
/// </summary>
public enum AssignmentStatus
{
    /// <summary>
    /// Asignación vigente.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Asignación finalizada.
    /// </summary>
    Inactive = 1
}
