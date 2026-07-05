using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Notifications.Senders;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Notifications;

/// <summary>
/// Pruebas del mapeo dominio→payload de FCM en <see cref="FirebaseCloudMessagingSender"/>. No valida
/// la integración con Firebase (sin red); protege contra regresiones en la construcción del mensaje.
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
}
