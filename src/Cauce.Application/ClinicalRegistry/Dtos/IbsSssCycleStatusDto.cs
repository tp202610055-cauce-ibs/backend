namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Estado del ciclo vigente del cuestionario IBS-SSS de un paciente.
/// </summary>
public enum IbsSssCycleStatus
{
    /// <summary>El paciente todavía no tiene un ciclo agendado.</summary>
    None,

    /// <summary>El cuestionario está agendado y sin responder.</summary>
    Pending,

    /// <summary>Pasaron 7 días desde el vencimiento sin respuesta: el ciclo quedó como no completado.</summary>
    Missed,

    /// <summary>El paciente respondió el cuestionario del ciclo.</summary>
    Completed
}

/// <summary>
/// Ciclo vigente del cuestionario IBS-SSS de un paciente, para su nutricionista.
/// </summary>
/// <param name="Status">Estado del ciclo.</param>
/// <param name="DueDate">Fecha en que vence el cuestionario, en UTC, o <see langword="null"/> sin ciclo.</param>
/// <param name="ReminderSentAt">Momento en que se envió el recordatorio, en UTC, o <see langword="null"/>.</param>
/// <param name="CompletedAt">Momento en que se respondió, en UTC, o <see langword="null"/>.</param>
public sealed record IbsSssCycleStatusDto(
    IbsSssCycleStatus Status,
    DateTime? DueDate,
    DateTime? ReminderSentAt,
    DateTime? CompletedAt);
