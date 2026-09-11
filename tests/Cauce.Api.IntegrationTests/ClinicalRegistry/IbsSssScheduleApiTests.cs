using System.Net;
using System.Net.Http.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Notifications;
using Cauce.Infrastructure.ClinicalRegistry;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración de las agendas de evaluaciones IBS-SSS (US12 CA02): al registrar una
/// evaluación se agenda la siguiente; el procesador envía el recordatorio a las 48 horas de vencida y
/// marca como perdida la que supera los 7 días.
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

    private async Task<(int RemindersSent, int MarkedMissed)> RunProcessorAsync()
    {
        var (scope, _) = CreateDbScope();
        using var _scope = scope;
        var processor = scope.ServiceProvider.GetRequiredService<IbsSssScheduleProcessor>();
        return await processor.ProcessAsync(DateTime.UtcNow);
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

        var (reminders, _) = await RunProcessorAsync();

        reminders.Should().Be(1);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var schedule = await db.IbsSssSchedules.AsNoTracking().SingleAsync(s => s.Id == scheduleId);
        schedule.ReminderSentAt.Should().NotBeNull();
        (await db.Set<Notification>().AsNoTracking().CountAsync(n => n.UserId == patient.Id)).Should().Be(1);

        // Un segundo pase no reenvía (reminder_sent_at ya está fijado).
        var (remindersAgain, _) = await RunProcessorAsync();
        remindersAgain.Should().Be(0);
    }

    [SkippableFact]
    public async Task Processor_ScheduleOverdue7Days_MarksMissed()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var scheduleId = await SeedScheduleAsync(patient.Id, DateTime.UtcNow.AddDays(-8));

        var (_, missed) = await RunProcessorAsync();

        missed.Should().Be(1);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var schedule = await db.IbsSssSchedules.AsNoTracking().SingleAsync(s => s.Id == scheduleId);
        schedule.Missed.Should().BeTrue();
    }
}
