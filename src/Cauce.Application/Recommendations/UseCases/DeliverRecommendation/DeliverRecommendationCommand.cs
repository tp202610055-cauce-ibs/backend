using Cauce.Application.Common.Idempotency;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.DeliverRecommendation;

/// <summary>
/// Comando para marcar una recomendación como entregada cuando el paciente la visualiza en la
/// aplicación. Es idempotente respecto del <see cref="ClientGuid"/> tomado del header
/// <c>Idempotency-Key</c>. El paciente se resuelve del JWT.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación a entregar.</param>
/// <param name="ClientGuid">Clave de idempotencia (UUID v4).</param>
public sealed record DeliverRecommendationCommand(
    Guid RecommendationId,
    Guid ClientGuid) : IRequest<Unit>, IIdempotentCommand;
