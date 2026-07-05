using Cauce.Application.Common.Messaging;
using Cauce.Domain.Recommendations.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Recommendations.EventHandlers;

/// <summary>
/// Registra la expiración de una recomendación.
/// </summary>
public sealed class RecommendationExpiredEventHandler
    : INotificationHandler<DomainEventNotification<RecommendationExpiredEvent>>
{
    private readonly ILogger<RecommendationExpiredEventHandler> _logger;

    /// <summary>
    /// Inicializa el handler con su logger.
    /// </summary>
    /// <param name="logger">Logger de la categoría del handler.</param>
    public RecommendationExpiredEventHandler(ILogger<RecommendationExpiredEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task Handle(
        DomainEventNotification<RecommendationExpiredEvent> notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Recommendation {RecommendationId} expired (previous status: {PreviousStatus}).",
            notification.DomainEvent.RecommendationId,
            notification.DomainEvent.PreviousStatus);
        return Task.CompletedTask;
    }
}
