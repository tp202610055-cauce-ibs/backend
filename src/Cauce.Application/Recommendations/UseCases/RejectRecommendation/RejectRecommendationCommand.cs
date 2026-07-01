using Cauce.Application.Common.Idempotency;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.RejectRecommendation;

/// <summary>
/// Comando para que un nutricionista rechace una recomendación con su motivo. Es idempotente
/// respecto del <see cref="ClientGuid"/> tomado del header <c>Idempotency-Key</c>. El
/// nutricionista se resuelve del JWT.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación a rechazar.</param>
/// <param name="Reason">Motivo del rechazo.</param>
/// <param name="ClientGuid">Clave de idempotencia (UUID v4).</param>
public sealed record RejectRecommendationCommand(
    Guid RecommendationId,
    string Reason,
    Guid ClientGuid) : IRequest<Unit>, IIdempotentCommand;
