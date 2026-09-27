using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Common.Messaging;
using Cauce.Application.Recommendations.EventHandlers;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Domain.Recommendations.Events;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Pruebas de <see cref="RecommendationRejectedEventHandler"/>. El motivo de un rechazo nunca le llega al
/// paciente (HU0017 CA2): tampoco por el push (acta A69).
/// </summary>
public sealed class RecommendationRejectedEventHandlerTests
{
    private const string Reason = "Motivo clínico interno que el paciente no debe leer.";

    private readonly INotificationScheduler _scheduler = Substitute.For<INotificationScheduler>();

    [Fact]
    public async Task Handle_Rejection_SchedulesAPushWithoutTheReason()
    {
        Notification? scheduled = null;
        await _scheduler.ScheduleAsync(Arg.Do<Notification>(notification => scheduled = notification), Arg.Any<CancellationToken>());
        var domainEvent = new RecommendationRejectedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Reason, DateTime.UtcNow);

        await new RecommendationRejectedEventHandler(_scheduler)
            .Handle(new DomainEventNotification<RecommendationRejectedEvent>(domainEvent), CancellationToken.None);

        scheduled.Should().NotBeNull();
        scheduled!.UserId.Should().Be(domainEvent.PatientId);
        scheduled.Channel.Should().Be(NotificationChannel.Push);
        scheduled.Title.Should().NotContain(Reason);
        scheduled.Body.Should().NotContain(Reason);
    }
}
