using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Messaging;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Events;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.EventHandlers;

/// <summary>
/// Cuando un paciente envía una evaluación IBS-SSS (línea base US04 o periódica US12), agenda una
/// notificación por correo al nutricionista asignado, con el mensaje adecuado según el tipo de
/// evaluación. Es idempotente respecto del reprocesamiento del outbox (DEC-B5-05).
/// </summary>
public sealed class IbsSssAssessmentSubmittedEventHandler
    : INotificationHandler<DomainEventNotification<IbsSssAssessmentSubmittedEvent>>
{
    private const string RelatedEntityType = "ibs_sss_assessment";

    private readonly INotificationScheduler _scheduler;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    /// <param name="scheduler">Agendador de notificaciones.</param>
    /// <param name="nutritionistPatientRepository">Repositorio de asignaciones nutricionista-paciente.</param>
    public IbsSssAssessmentSubmittedEventHandler(
        INotificationScheduler scheduler,
        INutritionistPatientRepository nutritionistPatientRepository)
    {
        _scheduler = scheduler;
        _nutritionistPatientRepository = nutritionistPatientRepository;
    }

    /// <inheritdoc />
    public async Task Handle(
        DomainEventNotification<IbsSssAssessmentSubmittedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var assignment = await _nutritionistPatientRepository
            .FindActiveByPatientAsync(domainEvent.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (assignment is null)
        {
            return;
        }

        var exists = await _scheduler
            .ExistsForRelatedAsync(assignment.NutritionistId, RelatedEntityType, domainEvent.AssessmentId, NotificationType.Alert, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        var isBaseline = domainEvent.AssessmentType == AssessmentType.Baseline;
        var title = isBaseline
            ? "Nueva evaluación IBS-SSS de línea base"
            : "Nuevo IBS-SSS periódico de un paciente";
        var body = isBaseline
            ? $"Un paciente de tu seguimiento completó su evaluación IBS-SSS de línea base (puntaje {domainEvent.TotalScore}/500)."
            : $"Un paciente de tu seguimiento envió una nueva evaluación IBS-SSS periódica (puntaje {domainEvent.TotalScore}/500).";

        var email = Notification.Schedule(
            assignment.NutritionistId,
            NotificationType.Alert,
            NotificationChannel.Email,
            title,
            body,
            DateTime.UtcNow,
            RelatedEntityType,
            domainEvent.AssessmentId);

        await _scheduler.ScheduleAsync(email, cancellationToken).ConfigureAwait(false);
    }
}
