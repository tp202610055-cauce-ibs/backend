using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Notifications.Senders;
using FluentAssertions;
using MessagingErrorCode = FirebaseAdmin.Messaging.MessagingErrorCode;

namespace Cauce.Infrastructure.Tests.Notifications;

/// <summary>
/// Pruebas del mapeo dominio→payload de FCM y de la clasificación de errores de token inválido en
/// <see cref="FirebaseCloudMessagingSender"/>. No valida la integración con Firebase (sin red).
/// </summary>
public sealed class FirebaseCloudMessagingSenderTests
{
    [Fact]
    public void BuildMessage_MapsTokenTitleAndBody()
    {
        var notification = Notification.Schedule(
            Guid.NewGuid(), NotificationType.Recommendation, NotificationChannel.Push,
            "Tu recomendación está lista", "Revisa tu nueva recomendación dietética.", DateTime.UtcNow);

        var message = FirebaseCloudMessagingSender.BuildMessage("device-token-abc", notification);

        message.Token.Should().Be("device-token-abc");
        message.Notification.Title.Should().Be("Tu recomendación está lista");
        message.Notification.Body.Should().Be("Revisa tu nueva recomendación dietética.");
    }

    [Theory]
    [InlineData(MessagingErrorCode.Unregistered, true)]
    [InlineData(MessagingErrorCode.InvalidArgument, true)]
    [InlineData(MessagingErrorCode.Internal, false)]
    [InlineData(MessagingErrorCode.Unavailable, false)]
    public void IsInvalidTokenError_ClassifiesTokenErrors(MessagingErrorCode code, bool expected)
    {
        FirebaseCloudMessagingSender.IsInvalidTokenError(code).Should().Be(expected);
    }

    [Fact]
    public void IsInvalidTokenError_Null_ReturnsFalse()
    {
        FirebaseCloudMessagingSender.IsInvalidTokenError(null).Should().BeFalse();
    }
}
