using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Messaging;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Domain.Recommendations.Events;
using MediatR;

namespace Cauce.Application.Recommendations.EventHandlers;

/// <summary>
/// Al rechazarse una recomendación, agenda una notificación push para el paciente sin exponer el
/// motivo clínico. Es idempotente (DEC-B5-05).
/// </summary>
public sealed class RecommendationRejectedEventHandler
    : INotificationHandler<DomainEventNotification<RecommendationRejectedEvent>>
{
    private const string RelatedEntityType = "recommendation";

    private readonly INotificationScheduler _scheduler;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    /// <param name="scheduler">Agendador de notificaciones.</param>
    public RecommendationRejectedEventHandler(INotificationScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <inheritdoc />
    public async Task Handle(
        DomainEventNotification<RecommendationRejectedEvent> notification,
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
            "Se generó una nueva recomendación para ti",
            "Tu nutricionista está revisando tus recomendaciones; te contactaremos con más detalles.",
            DateTime.UtcNow,
            RelatedEntityType,
            domainEvent.RecommendationId);

        await _scheduler.ScheduleAsync(push, cancellationToken).ConfigureAwait(false);
    }
}
