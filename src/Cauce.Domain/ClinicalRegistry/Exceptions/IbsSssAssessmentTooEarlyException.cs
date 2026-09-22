using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando el paciente envía una evaluación IBS-SSS periódica antes de la fecha agendada,
/// más allá de la tolerancia de <see cref="IbsSssAssessmentSchedule.EarlySubmissionTolerance"/>.
/// Aceptarla acortaría el ciclo de 14 días del protocolo y haría incomparables las mediciones.
/// </summary>
public sealed class IbsSssAssessmentTooEarlyException : DomainException
{
    /// <summary>
    /// Fecha de vencimiento de la evaluación agendada, en UTC.
    /// </summary>
    public DateTime DueDate { get; }

    /// <summary>
    /// Momento, en UTC, a partir del cual se acepta el envío.
    /// </summary>
    public DateTime AcceptedFrom { get; }

    /// <summary>
    /// Inicializa la excepción con las fechas de la agenda vigente.
    /// </summary>
    /// <param name="dueDate">Fecha de vencimiento de la evaluación agendada.</param>
    /// <param name="acceptedFrom">Momento a partir del cual se acepta el envío.</param>
    public IbsSssAssessmentTooEarlyException(DateTime dueDate, DateTime acceptedFrom)
        : base($"La próxima evaluación IBS-SSS vence el {dueDate:yyyy-MM-dd} y no se acepta antes del {acceptedFrom:yyyy-MM-dd HH:mm} UTC.")
    {
        DueDate = dueDate;
        AcceptedFrom = acceptedFrom;
    }
}
