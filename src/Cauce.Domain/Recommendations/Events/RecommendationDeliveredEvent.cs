using Cauce.Domain.Common;

namespace Cauce.Domain.Recommendations.Events;

/// <summary>
/// Evento de dominio emitido cuando se entrega una recomendación al paciente. Definido para
/// uso futuro (Prompt 5); en Prompt 4 no se emite ni despacha.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación entregada.</param>
/// <param name="PatientId">Identificador del paciente destinatario.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record RecommendationDeliveredEvent(
    Guid RecommendationId,
    Guid PatientId,
    DateTime OccurredOn) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
