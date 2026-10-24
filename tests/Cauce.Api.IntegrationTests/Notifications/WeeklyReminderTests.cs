using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.Workers;
using Cauce.Application.Common.Notifications;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cauce.Api.IntegrationTests.Notifications;

/// <summary>
/// Pruebas de <see cref="WeeklyRecommendationReminderWorker"/>: el recordatorio de cada paciente activo usa
/// el texto neutro, sin datos clínicos. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class WeeklyReminderTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public WeeklyReminderTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task ScheduleReminders_ActivePatientOnly_GetsNeutralPushText()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var pendingPatient = await SeedPatientAsync();
        var (seedScope, seedDb) = CreateDbScope();
        using (seedScope)
        {
            // El paciente sembrado queda pendiente; verificar el correo lo activa, como en el alta real.
            (await seedDb.Users.SingleAsync(u => u.Id == patient.Id)).VerifyEmail();
            await seedDb.SaveChangesAsync();
        }

        var worker = new WeeklyRecommendationReminderWorker(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Factory.Services.GetRequiredService<IConfiguration>(),
            Factory.Services.GetRequiredService<ILogger<WeeklyRecommendationReminderWorker>>());

        await worker.ScheduleRemindersAsync(CancellationToken.None);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var reminder = await db.Set<Notification>().AsNoTracking().SingleAsync(n => n.UserId == patient.Id);
        reminder.Type.Should().Be(NotificationType.Reminder);
        reminder.Channel.Should().Be(NotificationChannel.Push);
        reminder.Title.Should().Be("Cauce");
        reminder.Body.Should().Be("Recuerda registrar tu diario esta semana.");
        reminder.Body.Should().Be(PatientPushTexts.WeeklyReminderBody);
        (await db.Set<Notification>().AsNoTracking().AnyAsync(n => n.UserId == pendingPatient.Id)).Should().BeFalse();
    }
}
