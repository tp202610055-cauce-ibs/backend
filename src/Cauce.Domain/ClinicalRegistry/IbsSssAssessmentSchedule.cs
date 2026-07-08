using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Agenda de una evaluación IBS-SSS periódica del paciente (US12 CA02). Modela como concepto separado
/// de la evaluación completada (acta A28): registra cuándo vence la próxima evaluación, si se completó o
/// se perdió, y si ya se envió el recordatorio. El worker de recordatorios avisa a las 48 horas de
/// vencida y la marca como perdida a los 7 días.
/// </summary>
public sealed class IbsSssAssessmentSchedule : Entity, IAggregateRoot
{
    /// <summary>
    /// Identificador del paciente al que corresponde la agenda.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Fecha de vencimiento de la evaluación, en UTC (medianoche del día de vencimiento).
    /// </summary>
    public DateTime DueDate { get; private set; }

    /// <summary>
    /// Indica si la evaluación agendada se completó.
    /// </summary>
    public bool Completed { get; private set; }

    /// <summary>
    /// Indica si la evaluación agendada se dio por perdida (no registrada a tiempo).
    /// </summary>
    public bool Missed { get; private set; }

    /// <summary>
    /// Momento en que se envió el recordatorio, o <see langword="null"/> si aún no se envió.
    /// </summary>
    public DateTime? ReminderSentAt { get; private set; }

    /// <summary>
    /// Momento en que se completó la evaluación, o <see langword="null"/>.
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Momento de creación, en UTC.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Indica si la agenda sigue abierta: ni completada ni perdida.
    /// </summary>
    public bool IsOpen => !Completed && !Missed;

    private IbsSssAssessmentSchedule()
    {
    }

    private IbsSssAssessmentSchedule(Guid id, Guid patientId, DateTime dueDate, DateTime utcNow)
        : base(id)
    {
        PatientId = patientId;
        DueDate = dueDate;
        Completed = false;
        Missed = false;
        CreatedAt = utcNow;
    }

    /// <summary>
    /// Crea una nueva agenda de evaluación IBS-SSS para un paciente.
    /// </summary>
    /// <param name="id">Identificador de la agenda.</param>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="dueDate">Fecha de vencimiento en UTC.</param>
    /// <param name="utcNow">Marca de tiempo UTC de creación.</param>
    /// <returns>La nueva agenda.</returns>
    public static IbsSssAssessmentSchedule Create(Guid id, Guid patientId, DateTime dueDate, DateTime utcNow)
    {
        return new IbsSssAssessmentSchedule(id, patientId, dueDate, utcNow);
    }

    /// <summary>
    /// Marca la agenda como completada. Es idempotente.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de finalización.</param>
    public void MarkCompleted(DateTime utcNow)
    {
        if (Completed)
        {
            return;
        }

        Completed = true;
        CompletedAt = utcNow;
    }

    /// <summary>
    /// Registra que ya se envió el recordatorio de la agenda.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC del envío.</param>
    public void MarkReminderSent(DateTime utcNow)
    {
        ReminderSentAt = utcNow;
    }

    /// <summary>
    /// Marca la agenda como perdida por no haberse completado a tiempo.
    /// </summary>
    public void MarkMissed()
    {
        if (Completed)
        {
            return;
        }

        Missed = true;
    }
}
