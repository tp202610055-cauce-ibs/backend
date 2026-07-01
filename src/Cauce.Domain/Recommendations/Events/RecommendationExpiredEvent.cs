using Cauce.Domain.Common;
using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Domain.Recommendations.Events;

/// <summary>
/// Evento de dominio emitido cuando una recomendación expira. Definido para uso futuro
/// (Prompt 5); en Prompt 4 no se emite ni despacha.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación expirada.</param>
/// <param name="PatientId">Identificador del paciente destinatario.</param>
/// <param name="PreviousStatus">Estado previo a la expiración.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record RecommendationExpiredEvent(
    Guid RecommendationId,
    Guid PatientId,
    RecommendationStatus PreviousStatus,
    DateTime OccurredOn) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
