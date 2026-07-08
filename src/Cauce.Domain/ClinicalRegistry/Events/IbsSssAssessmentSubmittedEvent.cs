using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry.Events;

/// <summary>
/// Evento de dominio emitido cuando un paciente envía una evaluación IBS-SSS, sea de línea base (US04)
/// o periódica (US12). Permite notificar al nutricionista asignado; el destinatario decide el mensaje
/// según el <see cref="AssessmentType"/>.
/// </summary>
/// <param name="AssessmentId">Identificador de la evaluación.</param>
/// <param name="UserId">Identificador de la cuenta del paciente.</param>
/// <param name="AssessmentType">Tipo de evaluación (línea base o periódica).</param>
/// <param name="TotalScore">Puntaje total IBS-SSS (0–500).</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record IbsSssAssessmentSubmittedEvent(
    Guid AssessmentId,
    Guid UserId,
    AssessmentType AssessmentType,
    int TotalScore,
    DateTime OccurredOn) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
