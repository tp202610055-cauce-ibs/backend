using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Messaging;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Domain.Recommendations.Events;
using MediatR;

namespace Cauce.Application.Recommendations.EventHandlers;

/// <summary>
/// Al crearse manualmente una recomendación (US29), agenda una notificación push para el paciente. Es
/// idempotente: no crea la notificación si ya existe una equivalente (DEC-B5-05).
/// </summary>
public sealed class RecommendationManuallyCreatedEventHandler
    : INotificationHandler<DomainEventNotification<RecommendationManuallyCreatedEvent>>
{
    private const string RelatedEntityType = "recommendation";

    private readonly INotificationScheduler _scheduler;

    /// <summary>
    /// Inicializa el handler con su agendador de notificaciones.
    /// </summary>
    /// <param name="scheduler">Agendador de notificaciones.</param>
    public RecommendationManuallyCreatedEventHandler(INotificationScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <inheritdoc />
    public async Task Handle(
        DomainEventNotification<RecommendationManuallyCreatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var exists = await _scheduler
            .ExistsForRelatedAsync(domainEvent.PatientId, RelatedEntityType, domainEvent.RecommendationId, NotificationType.Recommendation, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        var push = Notification.Schedule(
            domainEvent.PatientId,
            NotificationType.Recommendation,
            NotificationChannel.Push,
            "Tu nutricionista te preparó una recomendación",
            "Tocá para ver la recomendación que tu nutricionista creó para vos.",
            DateTime.UtcNow,
            RelatedEntityType,
            domainEvent.RecommendationId);

        await _scheduler.ScheduleAsync(push, cancellationToken).ConfigureAwait(false);
    }
}
