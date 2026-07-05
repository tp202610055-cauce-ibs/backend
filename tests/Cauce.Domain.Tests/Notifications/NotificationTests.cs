using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Domain.Notifications.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Notifications;

/// <summary>
/// Pruebas de la entidad <see cref="Notification"/>: máquina de estados y backoff exponencial.
/// </summary>
public sealed class NotificationTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Notification Schedule() => Notification.Schedule(
        Guid.NewGuid(), NotificationType.Recommendation, NotificationChannel.Push, "Título", "Cuerpo", Now,
        "recommendation", Guid.NewGuid());

    [Fact]
    public void Schedule_ValidData_CreatesPending()
    {
        var notification = Schedule();

        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.RetryCount.Should().Be((short)0);
        notification.ScheduledFor.Should().Be(Now);
    }

    [Fact]
    public void Schedule_EmptyTitle_Throws()
    {
        var act = () => Notification.Schedule(
            Guid.NewGuid(), NotificationType.Alert, NotificationChannel.Email, "  ", "Cuerpo", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsSent_FromPending_TransitionsToSent()
    {
        var notification = Schedule();

        notification.MarkAsSent(Now);

        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.SentAt.Should().Be(Now);
    }

    [Fact]
    public void MarkAsSent_NotPending_Throws()
    {
        var notification = Schedule();
        notification.MarkAsSent(Now);

        var act = () => notification.MarkAsSent(Now);

        act.Should().Throw<InvalidNotificationStateTransitionException>();
    }

    [Fact]
    public void MarkAsDelivered_FromSent_TransitionsToDelivered()
    {
        var notification = Schedule();
        notification.MarkAsSent(Now);

        notification.MarkAsDelivered(Now);

        notification.Status.Should().Be(NotificationStatus.Delivered);
    }

    [Fact]
    public void MarkAsDelivered_NotSent_Throws()
    {
        var notification = Schedule();

        var act = () => notification.MarkAsDelivered(Now);

        act.Should().Throw<InvalidNotificationStateTransitionException>();
    }

    [Fact]
    public void RecordFailedAttempt_FirstAttempt_ReschedulesWithOneMinuteBackoff()
    {
        var notification = Schedule();

        notification.RecordFailedAttempt("error transitorio", Now);

        notification.RetryCount.Should().Be((short)1);
        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.ScheduledFor.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void RecordFailedAttempt_ThirdAttempt_MarksFailed()
    {
        var notification = Schedule();

        notification.RecordFailedAttempt("e1", Now);
        notification.RecordFailedAttempt("e2", Now);
        notification.RecordFailedAttempt("e3", Now);

        notification.RetryCount.Should().Be((short)3);
        notification.Status.Should().Be(NotificationStatus.Failed);
    }

    [Fact]
    public void CanRetryNow_PendingDueWithRetries_ReturnsTrue()
    {
        var notification = Schedule();

        notification.CanRetryNow(Now).Should().BeTrue();
    }

    [Fact]
    public void CanRetryNow_ScheduledInFuture_ReturnsFalse()
    {
        var notification = Schedule();
        notification.RecordFailedAttempt("error", Now);

        notification.CanRetryNow(Now).Should().BeFalse();
        notification.CanRetryNow(Now.AddMinutes(2)).Should().BeTrue();
    }
}
