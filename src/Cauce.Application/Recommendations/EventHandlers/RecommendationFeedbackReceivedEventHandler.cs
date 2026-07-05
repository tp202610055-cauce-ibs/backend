using Cauce.Application.Common.Messaging;
using Cauce.Domain.Recommendations.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Recommendations.EventHandlers;

/// <summary>
/// Registra la recepción de retroalimentación de una recomendación.
/// </summary>
public sealed class RecommendationFeedbackReceivedEventHandler
    : INotificationHandler<DomainEventNotification<RecommendationFeedbackReceivedEvent>>
{
    private readonly ILogger<RecommendationFeedbackReceivedEventHandler> _logger;

    /// <summary>
    /// Inicializa el handler con su logger.
    /// </summary>
    /// <param name="logger">Logger de la categoría del handler.</param>
    public RecommendationFeedbackReceivedEventHandler(ILogger<RecommendationFeedbackReceivedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task Handle(
        DomainEventNotification<RecommendationFeedbackReceivedEvent> notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Feedback received for recommendation {RecommendationId} (outcome: {Outcome}).",
            notification.DomainEvent.RecommendationId,
            notification.DomainEvent.Outcome);
        return Task.CompletedTask;
    }
}
