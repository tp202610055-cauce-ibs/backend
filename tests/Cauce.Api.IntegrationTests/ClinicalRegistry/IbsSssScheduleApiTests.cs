using System.Net;
using System.Net.Http.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Application.Common.Notifications;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.ClinicalRegistry;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración de las agendas de evaluaciones IBS-SSS (US12 CA02): al registrar una
/// evaluación se agenda la siguiente; el procesador avisa el día del vencimiento, recuerda a las 48 horas
/// de vencida y marca como perdida la que supera los 7 días.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IbsSssScheduleApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public IbsSssScheduleApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private static object IbsSssBody(string assessmentType) => new
    {
        assessmentType,
        painSeverity = 40,
        painFrequency = 40,
        bloatingSeverity = 40,
        bowelHabitsDissatisfaction = 40,
        lifeInterference = 40
    };

    private async Task<Guid> SeedScheduleAsync(Guid patientId, DateTime dueDate)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var schedule = IbsSssAssessmentSchedule.Create(Guid.NewGuid(), patientId, dueDate, DateTime.UtcNow.AddDays(-20));
        db.IbsSssSchedules.Add(schedule);
        await db.SaveChangesAsync();
        return schedule.Id;
    }

    private async Task<IbsSssScheduleProcessingResult> RunProcessorAsync(DateTime? utcNow = null)
    {
        var (scope, _) = CreateDbScope();
        using var _scope = scope;
        var processor = scope.ServiceProvider.GetRequiredService<IbsSssScheduleProcessor>();
        return await processor.ProcessAsync(utcNow ?? DateTime.UtcNow);
    }

    private async Task<List<Notification>> NotificationsOfAsync(Guid patientId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Set<Notification>().AsNoTracking().Where(n => n.UserId == patientId).ToListAsync();
    }

    [SkippableFact]
    public async Task SubmitBaseline_CreatesNextSchedule()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/ibs-sss", IbsSssBody("Baseline"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var schedule = await db.IbsSssSchedules.AsNoTracking().SingleAsync(s => s.PatientId == patient.Id);
        schedule.Completed.Should().BeFalse();
        schedule.Missed.Should().BeFalse();
        schedule.DueDate.Should().BeAfter(DateTime.UtcNow.AddDays(10));
    }

    [SkippableFact]
    public async Task Processor_ScheduleOverdue48Hours_SendsReminderOnce()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var scheduleId = await SeedScheduleAsync(patient.Id, DateTime.UtcNow.AddDays(-3));

        var result = await RunProcessorAsync();

        result.RemindersSent.Should().Be(1);
        result.DueNoticesSent.Should().Be(0);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var schedule = await db.IbsSssSchedules.AsNoTracking().SingleAsync(s => s.Id == scheduleId);
        schedule.ReminderSentAt.Should().NotBeNull();
        var reminder = (await NotificationsOfAsync(patient.Id)).Should().ContainSingle().Subject;
        reminder.Type.Should().Be(NotificationType.Reminder);
        reminder.RelatedEntityType.Should().Be("ibs_sss_schedule");
        reminder.RelatedEntityId.Should().Be(scheduleId);
        reminder.Title.Should().Be("Cauce");
        reminder.Body.Should().Be(PatientPushTexts.FollowUpPendingBody);

        // Un segundo pase no reenvía (reminder_sent_at ya está fijado).
        (await RunProcessorAsync()).RemindersSent.Should().Be(0);
    }

    [SkippableFact]
    public async Task Processor_ScheduleOverdue7Days_MarksMissed()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var scheduleId = await SeedScheduleAsync(patient.Id, DateTime.UtcNow.AddDays(-8));

        var result = await RunProcessorAsync();

        result.MarkedMissed.Should().Be(1);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var schedule = await db.IbsSssSchedules.AsNoTracking().SingleAsync(s => s.Id == scheduleId);
        schedule.Missed.Should().BeTrue();
    }

    [SkippableFact]
    public async Task Processor_ScheduleDueTodayInLima_SendsOneDueNotice()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var scheduleId = await SeedScheduleAsync(patient.Id, DateTime.UtcNow.Date);

        var result = await RunProcessorAsync();

        result.DueNoticesSent.Should().Be(1);
        result.RemindersSent.Should().Be(0);
        var notice = (await NotificationsOfAsync(patient.Id)).Should().ContainSingle().Subject;
        notice.Type.Should().Be(NotificationType.Info);
        notice.Channel.Should().Be(NotificationChannel.Push);
        notice.RelatedEntityType.Should().Be("ibs_sss_schedule");
        notice.RelatedEntityId.Should().Be(scheduleId);
        notice.Title.Should().Be("Cauce");
        notice.Body.Should().Be(PatientPushTexts.FollowUpAvailableBody);

        // Un segundo pase el mismo día no repite el aviso.
        (await RunProcessorAsync()).DueNoticesSent.Should().Be(0);
        (await NotificationsOfAsync(patient.Id)).Should().ContainSingle();
    }

    [SkippableFact]
    public async Task Processor_ScheduleDueAfterTodayInLima_SendsNothing()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedScheduleAsync(patient.Id, DateTime.UtcNow.Date.AddDays(2));

        var result = await RunProcessorAsync();

        result.Should().Be(new IbsSssScheduleProcessingResult(0, 0, 0));
        (await NotificationsOfAsync(patient.Id)).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task Processor_DueNoticeThen48HourReminder_BothCoexistForTheSameSchedule()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var dueDate = DateTime.UtcNow.Date;
        var scheduleId = await SeedScheduleAsync(patient.Id, dueDate);

        var dueDay = await RunProcessorAsync(dueDate.AddHours(14));
        var twoDaysLater = await RunProcessorAsync(dueDate.AddHours(49));
        var threeDaysLater = await RunProcessorAsync(dueDate.AddHours(73));

        dueDay.DueNoticesSent.Should().Be(1);
        twoDaysLater.Should().Be(new IbsSssScheduleProcessingResult(1, 0, 0));
        threeDaysLater.Should().Be(new IbsSssScheduleProcessingResult(0, 0, 0));
        var notifications = await NotificationsOfAsync(patient.Id);
        notifications.Should().HaveCount(2);
        notifications.Should().OnlyContain(n => n.RelatedEntityType == "ibs_sss_schedule" && n.RelatedEntityId == scheduleId);
        notifications.Select(n => n.Type).Should().BeEquivalentTo(new[] { NotificationType.Info, NotificationType.Reminder });
    }

    [SkippableFact]
    public async Task Processor_CompletedSchedule_SendsNothing()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var scheduleId = await SeedScheduleAsync(patient.Id, DateTime.UtcNow.Date.AddDays(-3));
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var schedule = await db.IbsSssSchedules.SingleAsync(s => s.Id == scheduleId);
            schedule.MarkCompleted(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        (await RunProcessorAsync()).Should().Be(new IbsSssScheduleProcessingResult(0, 0, 0));
        (await NotificationsOfAsync(patient.Id)).Should().BeEmpty();
    }
}
