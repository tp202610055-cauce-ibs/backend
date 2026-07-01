using Cauce.Domain.Common;

namespace Cauce.Domain.Recommendations.Events;

/// <summary>
/// Evento de dominio emitido cuando se genera una recomendación. Definido para uso futuro
/// (Prompt 5: notificaciones y auditoría asíncrona); en Prompt 4 no se emite ni despacha.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación generada.</param>
/// <param name="PatientId">Identificador del paciente destinatario.</param>
/// <param name="ModelVersionId">Identificador de la versión de modelo usada.</param>
/// <param name="ConfidenceScore">Puntaje de confianza de la recomendación.</param>
/// <param name="RequiresReview">Indica si la recomendación requiere revisión humana.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record RecommendationGeneratedEvent(
    Guid RecommendationId,
    Guid PatientId,
    Guid ModelVersionId,
    decimal ConfidenceScore,
    bool RequiresReview,
    DateTime OccurredOn) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
