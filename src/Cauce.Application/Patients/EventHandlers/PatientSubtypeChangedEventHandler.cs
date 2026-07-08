using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Messaging;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Domain.Patients.Events;
using MediatR;

namespace Cauce.Application.Patients.EventHandlers;

/// <summary>
/// Cuando un paciente cambia su subtipo de SII, agenda una notificación por correo al nutricionista
/// asignado (US03 CA03). Es idempotente respecto del reprocesamiento del outbox (DEC-B5-05): usa el
/// identificador del evento como clave de la entidad relacionada.
/// </summary>
public sealed class PatientSubtypeChangedEventHandler
    : INotificationHandler<DomainEventNotification<PatientSubtypeChangedEvent>>
{
    private const string RelatedEntityType = "patient_subtype_change";

    private readonly INotificationScheduler _scheduler;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    /// <param name="scheduler">Agendador de notificaciones.</param>
    /// <param name="nutritionistPatientRepository">Repositorio de asignaciones nutricionista-paciente.</param>
    public PatientSubtypeChangedEventHandler(
        INotificationScheduler scheduler,
        INutritionistPatientRepository nutritionistPatientRepository)
    {
        _scheduler = scheduler;
        _nutritionistPatientRepository = nutritionistPatientRepository;
    }

    /// <inheritdoc />
    public async Task Handle(
        DomainEventNotification<PatientSubtypeChangedEvent> notification,
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
            .ExistsForRelatedAsync(assignment.NutritionistId, RelatedEntityType, domainEvent.Id, NotificationType.Alert, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        var email = Notification.Schedule(
            assignment.NutritionistId,
            NotificationType.Alert,
            NotificationChannel.Email,
            "Un paciente actualizó su subtipo de SII",
            $"Un paciente de tu seguimiento cambió su subtipo de SII de {domainEvent.OldSubtype} a {domainEvent.NewSubtype}. "
            + "Revisá su perfil en el portal para más detalles.",
            DateTime.UtcNow,
            RelatedEntityType,
            domainEvent.Id);

        await _scheduler.ScheduleAsync(email, cancellationToken).ConfigureAwait(false);
    }
}
