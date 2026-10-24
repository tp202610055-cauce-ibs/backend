using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Commands;

/// <summary>
/// Pruebas del comando <c>process-ibs-sss</c>: una pasada del procesador de agendas IBS-SSS a demanda, para
/// disparar los avisos sin esperar a las 09:00 de Lima. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProcessIbsSssCommandTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public ProcessIbsSssCommandTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task ProcessIbsSss_DueAndOverdueSchedules_SchedulesNoticeAndReminderAndExitsWithSuccess()
    {
        SkipIfUnavailable();
        var duePatient = await SeedPatientAsync();
        var overduePatient = await SeedPatientAsync();
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            db.IbsSssSchedules.Add(IbsSssAssessmentSchedule.Create(
                Guid.NewGuid(), duePatient.Id, DateTime.UtcNow.Date, DateTime.UtcNow.AddDays(-14)));
            db.IbsSssSchedules.Add(IbsSssAssessmentSchedule.Create(
                Guid.NewGuid(), overduePatient.Id, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-17)));
            await db.SaveChangesAsync();
        }

        var code = await CommandRunner.RunAsync(Factory.Services, ["process-ibs-sss"]);

        code.Should().Be(CommandRunner.Success);
        var (checkScope, checkDb) = CreateDbScope();
        using var _ = checkScope;
        var notifications = await checkDb.Set<Notification>().AsNoTracking()
            .Where(n => n.UserId == duePatient.Id || n.UserId == overduePatient.Id)
            .ToListAsync();
        notifications.Should().ContainSingle(n => n.UserId == duePatient.Id && n.Type == NotificationType.Info);
        notifications.Should().ContainSingle(n => n.UserId == overduePatient.Id && n.Type == NotificationType.Reminder);
        notifications.Should().HaveCount(2);
    }
}
