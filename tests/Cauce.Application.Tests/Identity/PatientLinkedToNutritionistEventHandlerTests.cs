using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Messaging;
using Cauce.Application.Identity.EventHandlers;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Events;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas de <see cref="PatientLinkedToNutritionistEventHandler"/>. Cubren la parametrización del
/// texto por contexto de vinculación (acta A43) y la idempotencia frente al reprocesamiento del outbox.
/// </summary>
public sealed class PatientLinkedToNutritionistEventHandlerTests
{
    private const string RegistrationBody =
        "Un nuevo paciente se registró con tu código de invitación y ya forma parte de tu seguimiento en Cauce.";

    private const string PostRegistrationBody =
        "Un paciente que ya tenía cuenta canjeó tu código de invitación y ya forma parte de tu seguimiento en Cauce.";

    private readonly INotificationScheduler _scheduler = Substitute.For<INotificationScheduler>();

    private PatientLinkedToNutritionistEventHandler CreateHandler() => new(_scheduler);

    private static DomainEventNotification<PatientLinkedToNutritionistEvent> Notification(
        PatientLinkedToNutritionistEvent domainEvent) => new(domainEvent);

    private static PatientLinkedToNutritionistEvent AnEvent(LinkContext? context = null) => context is null
        ? new PatientLinkedToNutritionistEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)
        : new PatientLinkedToNutritionistEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, context.Value);

    private async Task<Notification> CaptureScheduledAsync(PatientLinkedToNutritionistEvent domainEvent)
    {
        Notification? captured = null;
        await _scheduler.ScheduleAsync(
            Arg.Do<Notification>(n => captured = n),
            Arg.Any<CancellationToken>());

        await CreateHandler().Handle(Notification(domainEvent), CancellationToken.None);

        captured.Should().NotBeNull("el handler debe agendar una notificación");
        return captured!;
    }

    [Fact]
    public async Task Handle_RegistrationLink_UsesTheRegistrationBody()
    {
        var scheduled = await CaptureScheduledAsync(AnEvent(LinkContext.RegistrationLink));

        scheduled.Body.Should().Be(RegistrationBody);
    }

    [Fact]
    public async Task Handle_EventWithoutExplicitContext_UsesTheRegistrationBody()
    {
        // Construcción de cuatro argumentos, como la de los eventos ya persistidos en el outbox: el
        // valor por defecto preserva el texto original (acta A43).
        var scheduled = await CaptureScheduledAsync(AnEvent());

        scheduled.Body.Should().Be(RegistrationBody);
    }

    [Fact]
    public async Task Handle_PostRegistrationLink_UsesTheRedemptionBody()
    {
        var scheduled = await CaptureScheduledAsync(AnEvent(LinkContext.PostRegistrationLink));

        scheduled.Body.Should().Be(PostRegistrationBody);
        scheduled.Body.Should().NotBe(RegistrationBody);
    }

    [Fact]
    public async Task Handle_AnyContext_SchedulesAnEmailAlertToTheNutritionist()
    {
        var domainEvent = AnEvent(LinkContext.PostRegistrationLink);

        var scheduled = await CaptureScheduledAsync(domainEvent);

        scheduled.UserId.Should().Be(domainEvent.NutritionistUserId);
        scheduled.Type.Should().Be(NotificationType.Alert);
        scheduled.Channel.Should().Be(NotificationChannel.Email);
    }

    [Fact]
    public async Task Handle_NotificationAlreadyExists_DoesNotScheduleAgain()
    {
        _scheduler
            .ExistsForRelatedAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(),
                Arg.Any<NotificationType>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateHandler().Handle(Notification(AnEvent()), CancellationToken.None);

        // Exactly-once frente al reprocesamiento del outbox (DEC-B5-05).
        await _scheduler.DidNotReceive().ScheduleAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }
}
