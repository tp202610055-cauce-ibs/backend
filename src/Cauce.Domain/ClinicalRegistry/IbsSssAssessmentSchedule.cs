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
    /// Tolerancia con la que se acepta una evaluación periódica antes de su fecha de vencimiento.
    ///
    /// <para><b>Este es el único lugar donde se declara.</b> El protocolo del piloto todavía no fija
    /// una tolerancia, así que las 24 horas son un valor por defecto propuesto, no un dato clínico
    /// validado: cambiarlo es editar esta constante y nada más. La razón de que exista alguna
    /// tolerancia es que el ciclo de 14 días se ancla a medianoche UTC y el paciente responde en hora
    /// de Lima (UTC−5), así que un cuestionario contestado "el día que toca" puede caer unas horas
    /// antes del vencimiento sin estar adelantado en ningún sentido clínico.</para>
    /// </summary>
    public static readonly TimeSpan EarlySubmissionTolerance = TimeSpan.FromHours(24);

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
    /// Momento a partir del cual se acepta la evaluación agendada, aplicando la tolerancia.
    /// </summary>
    public DateTime AcceptedFrom => DueDate - EarlySubmissionTolerance;

    /// <summary>
    /// Indica si una evaluación enviada en el momento dado llega demasiado pronto respecto de esta
    /// agenda. Una agenda ya cerrada (completada o perdida) nunca rechaza: deja de gobernar el ciclo.
    /// </summary>
    /// <param name="utcNow">Momento del envío, en UTC.</param>
    /// <returns><see langword="true"/> si el envío se adelanta más allá de la tolerancia.</returns>
    public bool IsTooEarlyFor(DateTime utcNow)
    {
        return IsOpen && utcNow < AcceptedFrom;
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
