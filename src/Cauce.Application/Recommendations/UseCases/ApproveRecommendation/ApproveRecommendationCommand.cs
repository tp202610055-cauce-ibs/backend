using Cauce.Application.Common.Idempotency;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.ApproveRecommendation;

/// <summary>
/// Comando para que un nutricionista apruebe una recomendación con su nota clínica. Es
/// idempotente respecto del <see cref="ClientGuid"/> tomado del header <c>Idempotency-Key</c>.
/// El nutricionista se resuelve del JWT.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación a aprobar.</param>
/// <param name="Note">Nota clínica de aprobación.</param>
/// <param name="ClientGuid">Clave de idempotencia (UUID v4).</param>
public sealed record ApproveRecommendationCommand(
    Guid RecommendationId,
    string Note,
    Guid ClientGuid) : IRequest<Unit>, IIdempotentCommand;
