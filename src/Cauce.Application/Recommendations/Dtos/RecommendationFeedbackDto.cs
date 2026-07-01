using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.Dtos;

/// <summary>
/// Representación de la retroalimentación de una recomendación.
/// </summary>
/// <param name="FeedbackId">Identificador de la retroalimentación.</param>
/// <param name="WasApplied">Indica si el paciente aplicó la recomendación.</param>
/// <param name="Outcome">Resultado clínico percibido.</param>
/// <param name="Comment">Comentario del paciente, o <see langword="null"/>.</param>
/// <param name="SubmittedAt">Momento de envío, en UTC.</param>
public sealed record RecommendationFeedbackDto(
    Guid FeedbackId,
    bool WasApplied,
    FeedbackOutcome Outcome,
    string? Comment,
    DateTime SubmittedAt);
