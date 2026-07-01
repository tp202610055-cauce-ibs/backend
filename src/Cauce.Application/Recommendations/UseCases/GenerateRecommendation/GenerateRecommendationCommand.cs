using Cauce.Application.Common.Idempotency;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.GenerateRecommendation;

/// <summary>
/// Comando para generar una nueva recomendación dietética para el paciente autenticado. Es
/// idempotente respecto del <see cref="ClientGuid"/> tomado del header <c>Idempotency-Key</c>.
/// El paciente se resuelve del JWT; no se recibe del cliente.
/// </summary>
/// <param name="ClientGuid">Clave de idempotencia generada en el dispositivo (UUID v4).</param>
public sealed record GenerateRecommendationCommand(Guid ClientGuid)
    : IRequest<GenerateRecommendationResult>, IIdempotentCommand;
