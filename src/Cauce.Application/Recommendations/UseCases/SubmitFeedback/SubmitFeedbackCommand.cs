using Cauce.Application.Common.Idempotency;
using Cauce.Domain.Recommendations.Enums;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.SubmitFeedback;

/// <summary>
/// Comando para que el paciente envíe su retroalimentación sobre una recomendación entregada.
/// Es idempotente respecto del <see cref="ClientGuid"/> tomado del header <c>Idempotency-Key</c>.
/// El paciente se resuelve del JWT.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación.</param>
/// <param name="WasApplied">Indica si el paciente aplicó la recomendación.</param>
/// <param name="Outcome">Resultado clínico percibido.</param>
/// <param name="Comment">Comentario opcional.</param>
/// <param name="ClientGuid">Clave de idempotencia (UUID v4).</param>
public sealed record SubmitFeedbackCommand(
    Guid RecommendationId,
    bool WasApplied,
    FeedbackOutcome Outcome,
    string? Comment,
    Guid ClientGuid) : IRequest<Unit>, IIdempotentCommand;
