using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Messaging;
using Cauce.Domain.Identity.Events;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using MediatR;

namespace Cauce.Application.Identity.EventHandlers;

/// <summary>
/// Cuando un paciente se vincula a un nutricionista al registrarse con un código de invitación, agenda
/// una notificación por correo al nutricionista (US20 CA01). El destinatario viene en el propio evento,
/// por lo que no requiere resolver la asignación. Es idempotente respecto del reprocesamiento del outbox
/// (DEC-B5-05): usa el identificador del código de invitación como clave de la entidad relacionada.
/// </summary>
public sealed class PatientLinkedToNutritionistEventHandler
    : INotificationHandler<DomainEventNotification<PatientLinkedToNutritionistEvent>>
{
    private const string RelatedEntityType = "patient_link";

    private readonly INotificationScheduler _scheduler;

    /// <summary>
    /// Inicializa el handler con su agendador de notificaciones.
    /// </summary>
    /// <param name="scheduler">Agendador de notificaciones.</param>
    public PatientLinkedToNutritionistEventHandler(INotificationScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <inheritdoc />
    public async Task Handle(
        DomainEventNotification<PatientLinkedToNutritionistEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var exists = await _scheduler
            .ExistsForRelatedAsync(domainEvent.NutritionistUserId, RelatedEntityType, domainEvent.InvitationCodeId, NotificationType.Alert, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        var email = Notification.Schedule(
            domainEvent.NutritionistUserId,
            NotificationType.Alert,
            NotificationChannel.Email,
            "Nuevo paciente vinculado a tu seguimiento",
            "Un nuevo paciente se registró con tu código de invitación y ya forma parte de tu seguimiento en Cauce.",
            DateTime.UtcNow,
            RelatedEntityType,
            domainEvent.InvitationCodeId);

        await _scheduler.ScheduleAsync(email, cancellationToken).ConfigureAwait(false);
    }
}
