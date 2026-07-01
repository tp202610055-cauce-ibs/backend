using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Api.Contracts.Recommendations;

/// <summary>
/// Cuerpo de la petición para enviar retroalimentación sobre una recomendación.
/// </summary>
/// <param name="WasApplied">Indica si el paciente aplicó la recomendación.</param>
/// <param name="Outcome">Resultado clínico percibido.</param>
/// <param name="Comment">Comentario opcional (máximo 500 caracteres).</param>
public sealed record SubmitFeedbackRequest(
    bool WasApplied,
    FeedbackOutcome Outcome,
    string? Comment);
