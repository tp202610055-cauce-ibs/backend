namespace Cauce.Domain.Patients.Enums;

/// <summary>
/// Nivel de prioridad de atención de un paciente en el panel de triaje del
/// nutricionista (US18). Se calcula en la capa de aplicación a partir de la
/// severidad IBS-SSS más reciente, las recomendaciones pendientes de revisión
/// vencidas y la actividad reciente del paciente. Ordena el panel de mayor a
/// menor urgencia.
/// </summary>
public enum PriorityLevel
{
    /// <summary>
    /// Sin datos suficientes para priorizar (paciente sin línea base ni actividad).
    /// </summary>
    None = 0,

    /// <summary>
    /// Prioridad baja: el paciente tiene datos pero no requiere atención inmediata.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Prioridad media: severidad moderada o inactividad prolongada.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Prioridad alta: severidad severa o recomendaciones pendientes de revisión vencidas.
    /// </summary>
    High = 3
}
