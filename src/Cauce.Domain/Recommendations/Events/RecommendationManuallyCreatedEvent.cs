using Cauce.Domain.Common;

namespace Cauce.Domain.Recommendations.Events;

/// <summary>
/// Evento de dominio emitido cuando un nutricionista crea manualmente una recomendación (US29). Permite
/// notificar al paciente de forma asíncrona vía outbox.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación creada.</param>
/// <param name="PatientId">Identificador del paciente destinatario.</param>
/// <param name="NutritionistId">Identificador del nutricionista autor.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record RecommendationManuallyCreatedEvent(
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
