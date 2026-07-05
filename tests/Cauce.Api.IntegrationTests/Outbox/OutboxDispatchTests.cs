using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Outbox;
using Cauce.Domain.Outbox;
using Cauce.Domain.Recommendations.Events;
using Cauce.Infrastructure.Outbox;
using Cauce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Outbox;

/// <summary>
/// Pruebas del despacho de outbox (DEC-B5-04) vía <see cref="OutboxBatchProcessor"/>: publicación,
/// creación de la notificación, reintentos con backoff <b>respetado</b> (C4), envenenamiento y
/// exactly-once. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class OutboxDispatchTests : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public OutboxDispatchTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task ProcessAsync_ApprovedEvent_CreatesNotificationAndMarksProcessed()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var (messageId, recommendationId) = await PublishApprovedEventAsync(patient.Id);

        await ProcessOnceAsync(messageId);

        (await GetMessageAsync(messageId))!.ProcessedAt.Should().NotBeNull();
        (await NotificationCountAsync(patient.Id, recommendationId)).Should().Be(1);
    }

    [SkippableFact]
    public async Task ProcessAsync_UnregisteredEventType_RecordsFailedAttempt()
    {
        SkipIfUnavailable();
        var messageId = await InsertBogusMessageAsync();

        await ProcessOnceAsync(messageId);

        var message = await GetMessageAsync(messageId);
        message!.Attempts.Should().Be((short)1);
        message.ProcessedAt.Should().BeNull();
        message.NextAttemptAt.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task ProcessAsync_TenFailedAttempts_PoisonsMessage()
    {
        SkipIfUnavailable();
        var messageId = await InsertBogusMessageAsync();

        for (var i = 0; i < 10; i++)
        {
            await ProcessOnceAsync(messageId);
        }

        var message = await GetMessageAsync(messageId);
        message!.Attempts.Should().Be((short)10);
        message.ProcessedAt.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task ProcessAsync_AlreadyProcessed_IsSkipped()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var (messageId, _) = await PublishApprovedEventAsync(patient.Id);

        await ProcessOnceAsync(messageId);
        var firstProcessedAt = (await GetMessageAsync(messageId))!.ProcessedAt;
        await ProcessOnceAsync(messageId);

        (await GetMessageAsync(messageId))!.ProcessedAt.Should().Be(firstProcessedAt);
    }

    [SkippableFact]
    public async Task ProcessAsync_SameEventTwice_DoesNotDuplicateNotification()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var recommendationId = Guid.NewGuid();
        var first = await PublishApprovedEventAsync(patient.Id, recommendationId);
        var second = await PublishApprovedEventAsync(patient.Id, recommendationId);

        await ProcessOnceAsync(first.MessageId);
        await ProcessOnceAsync(second.MessageId);

        (await NotificationCountAsync(patient.Id, recommendationId)).Should().Be(1);
    }

    [SkippableFact]
    public async Task RetentionRepository_DeletesProcessedOlderThanCutoff()
    {
        SkipIfUnavailable();
        var messageId = await InsertBogusMessageAsync(processedAt: DateTime.UtcNow.AddDays(-40));

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var deleted = await repository.DeleteProcessedOlderThanAsync(DateTime.UtcNow.AddDays(-30));

        deleted.Should().BeGreaterThanOrEqualTo(1);
        (await db.OutboxMessages.AsNoTracking().AnyAsync(m => m.Id == messageId)).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Backoff_IsRespected_MessageNotEligibleUntilWindowExpires()
    {
        SkipIfUnavailable();
        var messageId = await InsertBogusMessageAsync();

        // Primer fallo: fija next_attempt_at = ahora + 2s (2^1) usando el reloj controlable.
        await ProcessOnceAsync(messageId);
        (await GetMessageAsync(messageId))!.Attempts.Should().Be((short)1);

        var now = Factory.Clock.GetUtcNow().UtcDateTime;
        (await PendingIdsAsync(now)).Should().NotContain(messageId);

        // Aún dentro de la ventana: sigue excluido y no se re-procesa.
        Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        (await PendingIdsAsync(Factory.Clock.GetUtcNow().UtcDateTime)).Should().NotContain(messageId);

        // Pasada la ventana (2s): vuelve a ser elegible y un nuevo procesamiento incrementa attempts.
        Factory.Clock.Advance(TimeSpan.FromSeconds(2));
        (await PendingIdsAsync(Factory.Clock.GetUtcNow().UtcDateTime)).Should().Contain(messageId);

        await ProcessOnceAsync(messageId);
        (await GetMessageAsync(messageId))!.Attempts.Should().Be((short)2);
    }

    // ----- Helpers -----

    private async Task<(Guid MessageId, Guid RecommendationId)> PublishApprovedEventAsync(Guid patientId, Guid? recommendationId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        var recId = recommendationId ?? Guid.NewGuid();
        var evt = new RecommendationApprovedEvent(recId, patientId, Guid.NewGuid(), AutoApproved: false, DateTime.UtcNow);
        await writer.PublishAsync(recId, "recommendation", evt);
        await unitOfWork.SaveChangesAsync();

        var messageId = await db.OutboxMessages.Where(m => m.AggregateId == recId).Select(m => m.Id).OrderBy(id => id).LastAsync();
        return (messageId, recId);
    }

    private async Task<Guid> InsertBogusMessageAsync(DateTime? processedAt = null)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var message = OutboxMessage.Publish(Guid.NewGuid(), "test", "Cauce.Tests.UnknownEvent", "{}", DateTime.UtcNow);
        if (processedAt is not null)
        {
            message.MarkAsProcessed(processedAt.Value);
        }

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    private async Task ProcessOnceAsync(Guid messageId)
    {
        using var scope = Factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>();
        await processor.ProcessAsync(messageId);
    }

    private async Task<OutboxMessage?> GetMessageAsync(Guid messageId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == messageId);
    }

    private async Task<IReadOnlyList<Guid>> PendingIdsAsync(DateTime now)
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        return await repository.ListPendingIdsAsync(now, 50);
    }

    private async Task<int> NotificationCountAsync(Guid userId, Guid relatedEntityId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.RelatedEntityId == relatedEntityId);
    }
}
