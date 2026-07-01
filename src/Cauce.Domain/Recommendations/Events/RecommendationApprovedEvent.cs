using Cauce.Domain.Common;

namespace Cauce.Domain.Recommendations.Events;

/// <summary>
/// Evento de dominio emitido cuando se aprueba una recomendación. Definido para uso futuro
/// (Prompt 5); en Prompt 4 no se emite ni despacha.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación aprobada.</param>
/// <param name="PatientId">Identificador del paciente destinatario.</param>
/// <param name="NutritionistId">Identificador del nutricionista revisor; vacío si fue auto-aprobada.</param>
/// <param name="AutoApproved">Indica si la aprobación fue automática.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record RecommendationApprovedEvent(
    Guid RecommendationId,
    Guid PatientId,
    Guid NutritionistId,
    bool AutoApproved,
    DateTime OccurredOn) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
