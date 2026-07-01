using Cauce.Domain.Common;

namespace Cauce.Domain.Recommendations.Events;

/// <summary>
/// Evento de dominio emitido cuando se rechaza una recomendación. Definido para uso futuro
/// (Prompt 5); en Prompt 4 no se emite ni despacha.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación rechazada.</param>
/// <param name="PatientId">Identificador del paciente destinatario.</param>
/// <param name="NutritionistId">Identificador del nutricionista que rechazó.</param>
/// <param name="Reason">Motivo del rechazo.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record RecommendationRejectedEvent(
    Guid RecommendationId,
    Guid PatientId,
    Guid NutritionistId,
    string Reason,
    DateTime OccurredOn) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
