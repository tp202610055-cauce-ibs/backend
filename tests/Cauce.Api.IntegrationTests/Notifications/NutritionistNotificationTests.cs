using System.Net;
using System.Net.Http.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Application.Common.Interfaces;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Events;
using Cauce.Domain.Common;
using Cauce.Domain.Identity.Events;
using Cauce.Domain.Notifications.Enums;
using Cauce.Domain.Patients.Events;
using Cauce.Infrastructure.Outbox;
using Cauce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Notifications;

/// <summary>
/// Pruebas de integración de las notificaciones al nutricionista (Item 8): publicación del evento de
/// dominio vía outbox, procesamiento por el dispatcher, creación de la notificación por correo para el
/// nutricionista asignado y exactly-once ante el reprocesamiento (US03 CA03, US04/US12 CA01, US20 CA01).
/// Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class NutritionistNotificationTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public NutritionistNotificationTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task UpdateSubtype_ViaApi_PublishesEventAndNotifiesNutritionist()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id); // subtipo inicial IBS-M
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var client = PatientClient(patient.KeycloakId, patient.Email);
        var response = await client.PutAsJsonAsync("/api/v1/patients/profile", new { ibsSubtype = "IbsD" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var messageIds = await OutboxIdsOfTypeAsync(typeof(PatientSubtypeChangedEvent).FullName!);
        messageIds.Should().ContainSingle();

        await ProcessOnceAsync(messageIds[0]);

        (await NutritionistAlertCountAsync(nutritionist.Id)).Should().Be(1);
    }

    [SkippableFact]
    public async Task UpdateSubtype_ViaApi_SameSubtype_DoesNotPublishEvent()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id); // subtipo inicial IBS-M
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var client = PatientClient(patient.KeycloakId, patient.Email);
        var response = await client.PutAsJsonAsync("/api/v1/patients/profile", new { ibsSubtype = "IbsM" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await OutboxIdsOfTypeAsync(typeof(PatientSubtypeChangedEvent).FullName!)).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task IbsSssAssessmentSubmitted_NotifiesNutritionist_ExactlyOnce()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var assessmentId = Guid.NewGuid();
        IbsSssAssessmentSubmittedEvent Event() =>
            new(assessmentId, patient.Id, AssessmentType.Baseline, 220, DateTime.UtcNow);

        var first = await PublishEventAsync(assessmentId, "IbsSssAssessment", Event());
        var second = await PublishEventAsync(assessmentId, "IbsSssAssessment", Event());

        await ProcessOnceAsync(first);
        await ProcessOnceAsync(second);

        // Exactly-once: dos mensajes de outbox con el mismo evento producen una sola notificación.
        (await NutritionistAlertCountAsync(nutritionist.Id)).Should().Be(1);
    }

    [SkippableFact]
    public async Task PatientLinkedToNutritionist_NotifiesNutritionist()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        var patientUserId = Guid.NewGuid();
        var invitationCodeId = Guid.NewGuid();

        var messageId = await PublishEventAsync(
            patientUserId,
            "User",
            new PatientLinkedToNutritionistEvent(patientUserId, nutritionist.Id, invitationCodeId, DateTime.UtcNow));

        await ProcessOnceAsync(messageId);

        (await NutritionistAlertCountAsync(nutritionist.Id)).Should().Be(1);
    }

    // ----- Helpers -----

    private async Task<Guid> PublishEventAsync(Guid aggregateId, string aggregateType, IDomainEvent domainEvent)
    {
        using var scope = Factory.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        await writer.PublishAsync(aggregateId, aggregateType, domainEvent);
        await unitOfWork.SaveChangesAsync();

        return await db.OutboxMessages
            .Where(m => m.AggregateId == aggregateId)
            .OrderByDescending(m => m.OccurredAt)
            .Select(m => m.Id)
            .FirstAsync();
    }

    private async Task ProcessOnceAsync(Guid messageId)
    {
        using var scope = Factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>();
        await processor.ProcessAsync(messageId);
    }

    private async Task<IReadOnlyList<Guid>> OutboxIdsOfTypeAsync(string eventTypeFullName)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.OutboxMessages.AsNoTracking()
            .Where(m => m.EventType == eventTypeFullName)
            .Select(m => m.Id)
            .ToListAsync();
    }

    private async Task<int> NutritionistAlertCountAsync(Guid nutritionistId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == nutritionistId
                && n.Type == NotificationType.Alert
                && n.Channel == NotificationChannel.Email);
    }
}
