using Cauce.Domain.Common;
using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Domain.Recommendations.Events;

/// <summary>
/// Evento de dominio emitido cuando el paciente envía retroalimentación sobre una
/// recomendación. Definido para uso futuro (Prompt 5); en Prompt 4 no se emite ni despacha.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación.</param>
/// <param name="PatientId">Identificador del paciente que envió la retroalimentación.</param>
/// <param name="Outcome">Resultado clínico declarado.</param>
/// <param name="WasApplied">Indica si el paciente aplicó la recomendación.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record RecommendationFeedbackReceivedEvent(
    Guid RecommendationId,
    Guid PatientId,
    FeedbackOutcome Outcome,
    bool WasApplied,
    DateTime OccurredOn) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
