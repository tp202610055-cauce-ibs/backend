using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Messaging;
using Cauce.Domain.Identity.Events;
using Cauce.Domain.Notifications.Enums;
using MediatR;

namespace Cauce.Application.Notifications.EventHandlers;

/// <summary>
/// Deja en la bandeja del nutricionista el aviso de un paciente vinculado, tanto al registrarse con
/// código como desde el perfil. Es idempotente y no envía push.
/// </summary>
public sealed class PatientLinkedAlertEventHandler
    : INotificationHandler<DomainEventNotification<PatientLinkedToNutritionistEvent>>
{
    private readonly IUserRepository _userRepository;
    private readonly INutritionistAlertRepository _alertRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public PatientLinkedAlertEventHandler(IUserRepository userRepository, INutritionistAlertRepository alertRepository)
    {
        _userRepository = userRepository;
        _alertRepository = alertRepository;
    }

    /// <inheritdoc />
    public Task Handle(
        DomainEventNotification<PatientLinkedToNutritionistEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        return NutritionistAlertWriter.WriteAsync(
            _userRepository, _alertRepository, domainEvent.NutritionistUserId, domainEvent.PatientUserId,
            NutritionistAlertType.PatientLinked, domainEvent.OccurredOn, cancellationToken);
    }
}
