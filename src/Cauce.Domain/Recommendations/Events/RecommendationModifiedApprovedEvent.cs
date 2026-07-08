using Cauce.Domain.Common;

namespace Cauce.Domain.Recommendations.Events;

/// <summary>
/// Evento de dominio emitido cuando un nutricionista aprueba una recomendación tras modificarla
/// (US17 CA03). Permite notificar al paciente de forma asíncrona vía outbox.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación modificada y aprobada.</param>
/// <param name="PatientId">Identificador del paciente destinatario.</param>
/// <param name="NutritionistId">Identificador del nutricionista revisor.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record RecommendationModifiedApprovedEvent(
    Guid RecommendationId,
    Guid PatientId,
    Guid NutritionistId,
    DateTime OccurredOn) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
