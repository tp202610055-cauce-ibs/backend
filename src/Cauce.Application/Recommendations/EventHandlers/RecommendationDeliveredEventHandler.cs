using Cauce.Application.Common.Messaging;
using Cauce.Domain.Recommendations.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Recommendations.EventHandlers;

/// <summary>
/// Registra la entrega de una recomendación. No emite notificación adicional.
/// </summary>
public sealed class RecommendationDeliveredEventHandler
    : INotificationHandler<DomainEventNotification<RecommendationDeliveredEvent>>
{
    private readonly ILogger<RecommendationDeliveredEventHandler> _logger;

    /// <summary>
    /// Inicializa el handler con su logger.
    /// </summary>
    /// <param name="logger">Logger de la categoría del handler.</param>
    public RecommendationDeliveredEventHandler(ILogger<RecommendationDeliveredEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task Handle(
        DomainEventNotification<RecommendationDeliveredEvent> notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Recommendation {RecommendationId} delivered to patient {PatientId}.",
            notification.DomainEvent.RecommendationId,
            notification.DomainEvent.PatientId);
        return Task.CompletedTask;
    }
}
