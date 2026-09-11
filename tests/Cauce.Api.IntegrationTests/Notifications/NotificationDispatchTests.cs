using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Notifications;
using Cauce.Infrastructure.Notifications.Senders;
using Cauce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Notifications;

/// <summary>
/// Pruebas del despacho de notificaciones (DEC-B5-05) vía <see cref="NotificationBatchProcessor"/>:
/// email real por Mailpit, push por <see cref="FakeFcmSender"/> observable, backoff ante fallo, y el
/// orden determinista por <c>scheduled_for</c> y <c>notification_id</c> (C5). Requieren Docker
/// (PostgreSQL + Redis + Mailpit efímeros).
/// </summary>
[Trait("Category", "Integration")]
public sealed class NotificationDispatchTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IClassFixture<MailpitFixture>
{
    private readonly MailpitFixture _mailpit;

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public NotificationDispatchTests(PostgresFixture postgres, RedisFixture redis, MailpitFixture mailpit)
        : base(postgres, redis)
    {
        _mailpit = mailpit;
    }

    /// <inheritdoc />
    protected override bool ExtraAvailable => _mailpit.IsAvailable;

    /// <inheritdoc />
    protected override CustomWebApplicationFactory CreateFactory() => new(
        Postgres.ConnectionString,
        Redis.ConnectionString,
        smtpHost: _mailpit.SmtpHost,
        smtpPort: _mailpit.SmtpPort);

    [SkippableFact]
    public async Task DispatchDue_EmailNotification_DeliveredViaMailpitAndMarkedSent()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var id = await ScheduleAsync(patient.Id, NotificationChannel.Email, DateTime.UtcNow.AddMinutes(-1));

        await DispatchAsync(DateTime.UtcNow);

        (await StatusAsync(id)).Should().Be(NotificationStatus.Sent);
        (await MailpitTotalAsync()).Should().BeGreaterThanOrEqualTo(1);
    }

    [SkippableFact]
    public async Task DispatchDue_PushNotification_MarkedSentAndRecordedInFakeFcm()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var id = await ScheduleAsync(patient.Id, NotificationChannel.Push, DateTime.UtcNow.AddMinutes(-1));

        await DispatchAsync(DateTime.UtcNow);

        (await StatusAsync(id)).Should().Be(NotificationStatus.Sent);
        Fcm().SentMessages.Should().ContainSingle(m => m.NotificationId == id && m.UserId == patient.Id);
    }

    [SkippableFact]
    public async Task DispatchDue_PushFailure_RecordsFailedAttemptWithBackoff()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var scheduledFor = DateTime.UtcNow.AddMinutes(-1);
        var id = await ScheduleAsync(patient.Id, NotificationChannel.Push, scheduledFor);
        Fcm().SimulateFailureNextCall("invalid_token");

        var dispatchAt = DateTime.UtcNow;
        await DispatchAsync(dispatchAt);

        var notification = await GetAsync(id);
        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.RetryCount.Should().Be((short)1);
        // Backoff de 1 min tras el primer fallo (tolerancia por la precisión de timestamptz).
        notification.ScheduledFor.Should().BeCloseTo(dispatchAt.AddMinutes(1), TimeSpan.FromSeconds(1));
    }

    [SkippableFact]
    public async Task DispatchDue_ThreeFailures_MarksFailed()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var t0 = DateTime.UtcNow;
        var id = await ScheduleAsync(patient.Id, NotificationChannel.Push, t0);

        Fcm().SimulateFailureNextCall("e1");
        await DispatchAsync(t0);
        Fcm().SimulateFailureNextCall("e2");
        await DispatchAsync(t0.AddMinutes(2));
        Fcm().SimulateFailureNextCall("e3");
        await DispatchAsync(t0.AddMinutes(10));

        var notification = await GetAsync(id);
        notification.RetryCount.Should().Be((short)3);
        notification.Status.Should().Be(NotificationStatus.Failed);
    }

    [SkippableFact]
    public async Task DispatchDue_OrdersByScheduledFor()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var later = await ScheduleAsync(patient.Id, NotificationChannel.Push, DateTime.UtcNow.AddMinutes(-1));
        var earlier = await ScheduleAsync(patient.Id, NotificationChannel.Push, DateTime.UtcNow.AddMinutes(-5));

        await DispatchAsync(DateTime.UtcNow);

        var order = Fcm().SentMessages.Select(m => m.NotificationId).ToList();
        order.IndexOf(earlier).Should().BeLessThan(order.IndexOf(later));
    }

    [SkippableFact]
    public async Task DispatchDue_SameScheduledFor_OrdersByNotificationId()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var scheduledFor = DateTime.UtcNow.AddMinutes(-1);
        var a = await ScheduleAsync(patient.Id, NotificationChannel.Push, scheduledFor);
        var b = await ScheduleAsync(patient.Id, NotificationChannel.Push, scheduledFor);

        await DispatchAsync(DateTime.UtcNow);

        var expected = new[] { a, b }.OrderBy(id => id).ToList();
        var actual = Fcm().SentMessages.Select(m => m.NotificationId).Where(expected.Contains).ToList();
        actual.Should().Equal(expected);
    }

    // ----- Helpers -----

    private FakeFcmSender Fcm() => Factory.Services.GetRequiredService<FakeFcmSender>();

    private async Task<Guid> ScheduleAsync(Guid userId, NotificationChannel channel, DateTime scheduledFor)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var notification = Notification.Schedule(
            userId, NotificationType.Reminder, channel, "Recordatorio", "Cuerpo de la notificación.", scheduledFor);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification.Id;
    }

    private async Task DispatchAsync(DateTime now)
    {
        using var scope = Factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<NotificationBatchProcessor>();
        await processor.DispatchDueAsync(now, 50);
    }

    private async Task<Notification> GetAsync(Guid id)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Notifications.AsNoTracking().FirstAsync(n => n.Id == id);
    }

    private async Task<NotificationStatus> StatusAsync(Guid id) => (await GetAsync(id)).Status;

    private async Task<int> MailpitTotalAsync()
    {
        using var http = new HttpClient();
        var json = await http.GetStringAsync(_mailpit.MessagesApiUrl);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("total", out var total) ? total.GetInt32() : 0;
    }
}
