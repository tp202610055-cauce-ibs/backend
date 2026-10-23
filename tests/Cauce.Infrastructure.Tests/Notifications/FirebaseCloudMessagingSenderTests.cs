using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Notifications.Senders;
using FirebaseAdmin.Messaging;
using FluentAssertions;
using Notification = Cauce.Domain.Notifications.Notification;

namespace Cauce.Infrastructure.Tests.Notifications;

/// <summary>
/// Pruebas del mapeo dominio→payload de FCM y de la clasificación de errores de token inválido en
/// <see cref="FirebaseCloudMessagingSender"/>. No valida la integración con Firebase (sin red).
/// </summary>
public sealed class FirebaseCloudMessagingSenderTests
{
    private const string InvalidTokenMessage = "The registration token is not a valid FCM registration token";

    [Fact]
    public void BuildMessage_MapsTokenTitleAndBody()
    {
        var notification = Notification.Schedule(
            Guid.NewGuid(), NotificationType.Recommendation, NotificationChannel.Push,
            "Cauce", "Tienes una recomendación nueva. Toca para verla.", DateTime.UtcNow);

        var message = FirebaseCloudMessagingSender.BuildMessage("device-token-abc", notification);

        message.Token.Should().Be("device-token-abc");
        message.Notification.Title.Should().Be("Cauce");
        message.Notification.Body.Should().Be("Tienes una recomendación nueva. Toca para verla.");
    }

    [Fact]
    public void BuildMessage_WithRelatedEntity_AddsTypeAndEntityAsStrings()
    {
        var recommendationId = Guid.NewGuid();
        var notification = Notification.Schedule(
            Guid.NewGuid(), NotificationType.Recommendation, NotificationChannel.Push,
            "Cauce", "Cuerpo", DateTime.UtcNow, "recommendation", recommendationId);

        var message = FirebaseCloudMessagingSender.BuildMessage("device-token-abc", notification);

        message.Data.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["type"] = "recommendation",
            ["entityType"] = "recommendation",
            ["entityId"] = recommendationId.ToString()
        });
    }

    [Fact]
    public void BuildMessage_WithoutRelatedEntity_SendsEmptyEntityStrings()
    {
        var notification = Notification.Schedule(
            Guid.NewGuid(), NotificationType.Reminder, NotificationChannel.Push, "Cauce", "Cuerpo", DateTime.UtcNow);

        var message = FirebaseCloudMessagingSender.BuildMessage("device-token-abc", notification);

        message.Data["type"].Should().Be("reminder");
        message.Data["entityType"].Should().BeEmpty();
        message.Data["entityId"].Should().BeEmpty();
    }

    [Fact]
    public void BuildMessage_SetsHighPriorityAndAppChannelOnAndroid()
    {
        var notification = Notification.Schedule(
            Guid.NewGuid(), NotificationType.Reminder, NotificationChannel.Push, "Cauce", "Cuerpo", DateTime.UtcNow);

        var message = FirebaseCloudMessagingSender.BuildMessage("device-token-abc", notification);

        message.Android.Priority.Should().Be(Priority.High);
        message.Android.Notification.ChannelId.Should().Be("cauce_default");
        message.Notification.Should().NotBeNull();
    }

    [Theory]
    [InlineData(MessagingErrorCode.Unregistered, "Requested entity was not found.", true)]
    [InlineData(MessagingErrorCode.InvalidArgument, InvalidTokenMessage, true)]
    [InlineData(MessagingErrorCode.InvalidArgument, "Invalid value at 'message.android.notification.channel_id'", false)]
    [InlineData(MessagingErrorCode.InvalidArgument, null, false)]
    [InlineData(MessagingErrorCode.Internal, InvalidTokenMessage, false)]
    [InlineData(MessagingErrorCode.Unavailable, null, false)]
    public void IsInvalidTokenError_ClearsOnlyForTokenErrors(MessagingErrorCode code, string? message, bool expected)
    {
        FirebaseCloudMessagingSender.IsInvalidTokenError(code, message).Should().Be(expected);
    }

    [Fact]
    public void IsInvalidTokenError_Null_ReturnsFalse()
    {
        FirebaseCloudMessagingSender.IsInvalidTokenError(null, InvalidTokenMessage).Should().BeFalse();
    }
}
