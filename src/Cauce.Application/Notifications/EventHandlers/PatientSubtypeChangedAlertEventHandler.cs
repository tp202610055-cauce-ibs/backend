using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Messaging;
using Cauce.Domain.Notifications.Enums;
using Cauce.Domain.Patients.Events;
using MediatR;

namespace Cauce.Application.Notifications.EventHandlers;

/// <summary>
/// Deja en la bandeja del nutricionista activo el aviso de que su paciente cambió de subtipo de SII. El
/// evento solo se publica si el subtipo cambió de verdad. Es idempotente y no envía push.
/// </summary>
public sealed class PatientSubtypeChangedAlertEventHandler
    : INotificationHandler<DomainEventNotification<PatientSubtypeChangedEvent>>
{
    private readonly IUserRepository _userRepository;
    private readonly INutritionistAlertRepository _alertRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public PatientSubtypeChangedAlertEventHandler(
        IUserRepository userRepository,
        INutritionistAlertRepository alertRepository,
        INutritionistPatientRepository nutritionistPatientRepository)
    {
        _userRepository = userRepository;
        _alertRepository = alertRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
    }

    /// <inheritdoc />
    public async Task Handle(
        DomainEventNotification<PatientSubtypeChangedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        if (domainEvent.OldSubtype == domainEvent.NewSubtype)
        {
            return;
        }

        var assignment = await _nutritionistPatientRepository
            .FindActiveByPatientAsync(domainEvent.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (assignment is null)
        {
            return;
        }

        await NutritionistAlertWriter.WriteAsync(
            _userRepository, _alertRepository, assignment.NutritionistId, domainEvent.UserId,
            NutritionistAlertType.PatientSubtypeChanged, domainEvent.OccurredOn, cancellationToken).ConfigureAwait(false);
    }
}
