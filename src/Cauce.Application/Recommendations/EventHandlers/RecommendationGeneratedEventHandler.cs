using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Messaging;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Domain.Recommendations.Events;
using MediatR;

namespace Cauce.Application.Recommendations.EventHandlers;

/// <summary>
/// Al generarse una recomendación que requiere revisión, agenda una notificación por correo para el
/// nutricionista asignado al paciente. Es idempotente (DEC-B5-05).
/// </summary>
public sealed class RecommendationGeneratedEventHandler
    : INotificationHandler<DomainEventNotification<RecommendationGeneratedEvent>>
{
    private const string RelatedEntityType = "recommendation";

    private readonly INotificationScheduler _scheduler;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public RecommendationGeneratedEventHandler(
        INotificationScheduler scheduler,
        INutritionistPatientRepository nutritionistPatientRepository)
    {
        _scheduler = scheduler;
        _nutritionistPatientRepository = nutritionistPatientRepository;
    }

    /// <inheritdoc />
    public async Task Handle(
        DomainEventNotification<RecommendationGeneratedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        if (!domainEvent.RequiresReview)
        {
            return;
        }

        var assignment = await _nutritionistPatientRepository
            .FindActiveByPatientAsync(domainEvent.PatientId, cancellationToken)
            .ConfigureAwait(false);
        if (assignment is null)
        {
            return;
        }

        var exists = await _scheduler
            .ExistsForRelatedAsync(assignment.NutritionistId, RelatedEntityType, domainEvent.RecommendationId, NotificationType.Recommendation, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        var email = Notification.Schedule(
            assignment.NutritionistId,
            NotificationType.Recommendation,
            NotificationChannel.Email,
            "Nueva recomendación pendiente de revisión",
            "Un paciente tiene una recomendación esperando tu revisión en el portal de Cauce.",
            DateTime.UtcNow,
            RelatedEntityType,
            domainEvent.RecommendationId);

        await _scheduler.ScheduleAsync(email, cancellationToken).ConfigureAwait(false);
    }
}
