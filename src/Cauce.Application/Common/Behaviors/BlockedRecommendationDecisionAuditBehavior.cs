using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.UseCases;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Common.Exceptions;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Exceptions;
using MediatR;

namespace Cauce.Application.Common.Behaviors;

/// <summary>
/// Audita los intentos de aprobar, modificar, rechazar o archivar una recomendación cerrada que
/// terminan en 409. Registra la acción y el estado, nunca la nota, y vuelve a lanzar la excepción.
/// </summary>
public sealed class BlockedRecommendationDecisionAuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el behavior con sus dependencias.
    /// </summary>
    public BlockedRecommendationDecisionAuditBehavior(
        IRecommendationRepository recommendationRepository,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _recommendationRepository = recommendationRepository;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IRecommendationDecisionCommand decision)
        {
            return await next().ConfigureAwait(false);
        }

        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (DomainException exception) when (IsClosedConflict(exception))
        {
            await AuditAsync(decision, exception, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static bool IsClosedConflict(DomainException exception) => exception is
        InvalidRecommendationStateTransitionException or
        RecommendationExpiredException or
        RecommendationNotArchivableException;

    private async Task AuditAsync(IRecommendationDecisionCommand decision, DomainException exception, CancellationToken ct)
    {
        // El estado se lee de la entidad ya cargada por el handler; la transición rechazada no la modificó.
        var recommendation = await _recommendationRepository
            .GetByIdAsync(decision.RecommendationId, ct)
            .ConfigureAwait(false);
        var context = JsonSerializer.Serialize(new
        {
            attempted_action = decision.AttemptedAction,
            status = recommendation?.Status.ToString(),
            reason = exception.GetType().Name
        });

        await _auditLogger.LogAsync(
            AuditActionType.BlockedChangeAttempt,
            nameof(Recommendation),
            decision.RecommendationId,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: context,
            cancellationToken: ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
