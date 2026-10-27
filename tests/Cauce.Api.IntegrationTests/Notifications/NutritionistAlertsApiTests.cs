using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Application.Common.Interfaces;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Events;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Domain.Patients.Events;
using Cauce.Infrastructure.Outbox;
using Cauce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Notifications;

/// <summary>
/// Pruebas de la bandeja de avisos del nutricionista: qué eventos la llenan, sus rutas y el borrado de
/// los avisos de un paciente que da de baja su cuenta.
/// </summary>
[Trait("Category", "Integration")]
public sealed class NutritionistAlertsApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string ListUrl = "/api/v1/nutritionists/me/alerts";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public NutritionistAlertsApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    // ----- Generación -----

    [SkippableFact]
    public async Task LinkFromProfile_CreatesAPatientLinkedAlert()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        var code = await SeedInvitationAsync(nutritionist.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/patients/me/nutritionist-assignment", new { invitationCode = code });
        response.IsSuccessStatusCode.Should().BeTrue();
        await ProcessAllAsync(typeof(PatientLinkedToNutritionistEvent));

        var body = await ListAsync(nutritionist);
        body.GetProperty("unreadCount").GetInt32().Should().Be(1);
        var item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("type").GetString().Should().Be("PatientLinked");
        item.GetProperty("patientId").GetGuid().Should().Be(patient.Id);
        item.GetProperty("patientName").GetString().Should().NotBeNullOrWhiteSpace();
        item.GetProperty("isRead").GetBoolean().Should().BeFalse();
    }

    [SkippableFact]
    public async Task RegistrationLinkEvent_CreatesAPatientLinkedAlert()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();

        var messageId = await PublishEventAsync(patient.Id, new PatientLinkedToNutritionistEvent(
            patient.Id, nutritionist.Id, Guid.NewGuid(), DateTime.UtcNow, LinkContext.RegistrationLink));
        await ProcessOnceAsync(messageId);

        (await AlertsOfAsync(nutritionist.Id)).Should().ContainSingle(a => a.Type == NutritionistAlertType.PatientLinked);
    }

    [SkippableFact]
    public async Task SameEventProcessedTwice_CreatesASingleAlert()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        var linkedEvent = new PatientLinkedToNutritionistEvent(patient.Id, nutritionist.Id, Guid.NewGuid(), DateTime.UtcNow);

        var first = await PublishEventAsync(patient.Id, linkedEvent);
        var second = await PublishEventAsync(patient.Id, linkedEvent);
        await ProcessOnceAsync(first);
        await ProcessOnceAsync(second);

        (await AlertsOfAsync(nutritionist.Id)).Should().ContainSingle();
    }

    [SkippableFact]
    public async Task LinkEventForAnUnknownPatient_CreatesNoAlertAndIsProcessed()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        var unknown = Guid.NewGuid();

        var messageId = await PublishEventAsync(unknown, new PatientLinkedToNutritionistEvent(unknown, nutritionist.Id, Guid.NewGuid(), DateTime.UtcNow));
        await ProcessOnceAsync(messageId);

        (await AlertsOfAsync(nutritionist.Id)).Should().BeEmpty();
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == messageId)).ProcessedAt.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task SubtypeChange_WithActiveNutritionist_CreatesASubtypeAlert()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PutAsJsonAsync("/api/v1/patients/profile", new { ibsSubtype = "IbsC" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await ProcessAllAsync(typeof(PatientSubtypeChangedEvent));

        var alert = (await AlertsOfAsync(nutritionist.Id)).Should().ContainSingle().Subject;
        alert.Type.Should().Be(NutritionistAlertType.PatientSubtypeChanged);
        alert.PatientId.Should().Be(patient.Id);
    }

    [SkippableFact]
    public async Task SubtypeChange_WithoutNutritionist_CreatesNoAlert()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        await PatientClient(patient.KeycloakId, patient.Email).PutAsJsonAsync("/api/v1/patients/profile", new { ibsSubtype = "IbsC" });
        await ProcessAllAsync(typeof(PatientSubtypeChangedEvent));

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.NutritionistAlerts.AsNoTracking().CountAsync(a => a.PatientId == patient.Id)).Should().Be(0);
    }

    [SkippableFact]
    public async Task SameSubtype_CreatesNoAlert()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        await PatientClient(patient.KeycloakId, patient.Email).PutAsJsonAsync("/api/v1/patients/profile", new { ibsSubtype = "IbsM" });
        await ProcessAllAsync(typeof(PatientSubtypeChangedEvent));

        (await AlertsOfAsync(nutritionist.Id)).Should().BeEmpty();
    }

    // ----- Lectura -----

    [SkippableFact]
    public async Task List_ReturnsOnlyOwnAlertsNewestFirstWithUnreadCount()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        var other = await SeedNutritionistAsync();
        var older = await SeedAlertAsync(nutritionist.Id, patient.Id, DateTime.UtcNow.AddHours(-2));
        var newer = await SeedAlertAsync(nutritionist.Id, patient.Id, DateTime.UtcNow.AddHours(-1), read: true);
        await SeedAlertAsync(other.Id, patient.Id, DateTime.UtcNow);

        var body = await ListAsync(nutritionist);

        body.GetProperty("totalCount").GetInt32().Should().Be(2);
        body.GetProperty("unreadCount").GetInt32().Should().Be(1);
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Equal(newer, older);
    }

    [SkippableFact]
    public async Task List_Paginates()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        for (var i = 0; i < 3; i++)
        {
            await SeedAlertAsync(nutritionist.Id, patient.Id, DateTime.UtcNow.AddMinutes(-i));
        }

        var body = await ListAsync(nutritionist, "?page=2&pageSize=2");

        body.GetProperty("page").GetInt32().Should().Be(2);
        body.GetProperty("pageSize").GetInt32().Should().Be(2);
        body.GetProperty("totalCount").GetInt32().Should().Be(3);
        body.GetProperty("unreadCount").GetInt32().Should().Be(3);
        body.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [SkippableTheory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task List_InvalidPaging_Returns400(string query)
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email).GetAsync(ListUrl + query);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    // ----- Marcar como leído -----

    [SkippableFact]
    public async Task MarkRead_OwnAlert_Returns204AndIsIdempotent()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        var alertId = await SeedAlertAsync(nutritionist.Id, patient.Id, DateTime.UtcNow);
        var client = NutritionistClient(nutritionist.KeycloakId, nutritionist.Email);

        (await client.PostAsync($"{ListUrl}/{alertId}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"{ListUrl}/{alertId}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AlertsOfAsync(nutritionist.Id)).Single().IsRead.Should().BeTrue();
        (await ListAsync(nutritionist)).GetProperty("unreadCount").GetInt32().Should().Be(0);
    }

    [SkippableFact]
    public async Task MarkRead_AnotherNutritionistsAlert_Returns404AndLeavesItUnread()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var owner = await SeedNutritionistAsync();
        var intruder = await SeedNutritionistAsync();
        var alertId = await SeedAlertAsync(owner.Id, patient.Id, DateTime.UtcNow);

        var response = await NutritionistClient(intruder.KeycloakId, intruder.Email).PostAsync($"{ListUrl}/{alertId}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("nutritionist_alert_not_found");
        (await AlertsOfAsync(owner.Id)).Single().IsRead.Should().BeFalse();
    }

    [SkippableFact]
    public async Task MarkRead_UnknownAlert_Returns404()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email).PostAsync($"{ListUrl}/{Guid.NewGuid()}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task MarkRead_EmptyId_Returns400()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email).PostAsync($"{ListUrl}/{Guid.Empty}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task MarkAllRead_MarksOnlyOwnAlerts()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        var other = await SeedNutritionistAsync();
        await SeedAlertAsync(nutritionist.Id, patient.Id, DateTime.UtcNow.AddMinutes(-1));
        await SeedAlertAsync(nutritionist.Id, patient.Id, DateTime.UtcNow);
        await SeedAlertAsync(other.Id, patient.Id, DateTime.UtcNow);

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email).PostAsync($"{ListUrl}/read-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AlertsOfAsync(nutritionist.Id)).Should().OnlyContain(a => a.IsRead);
        (await AlertsOfAsync(other.Id)).Should().OnlyContain(a => !a.IsRead);
    }

    // ----- Acceso -----

    [SkippableTheory]
    [InlineData("GET", "")]
    [InlineData("POST", "/read-all")]
    [InlineData("POST", "/00000000-0000-0000-0000-000000000001/read")]
    public async Task AsPatient_Returns403(string method, string suffix)
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);

        var response = method == "GET" ? await client.GetAsync(ListUrl + suffix) : await client.PostAsync(ListUrl + suffix, null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableTheory]
    [InlineData("GET", "")]
    [InlineData("POST", "/read-all")]
    [InlineData("POST", "/00000000-0000-0000-0000-000000000001/read")]
    public async Task WithoutToken_Returns401(string method, string suffix)
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = method == "GET" ? await client.GetAsync(ListUrl + suffix) : await client.PostAsync(ListUrl + suffix, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ----- Baja del paciente -----

    [SkippableFact]
    public async Task PatientAccountDeletion_RemovesTheAlertsThatMentionThePatient()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var otherPatient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        await SeedAlertAsync(nutritionist.Id, patient.Id, DateTime.UtcNow.AddMinutes(-1));
        await SeedAlertAsync(nutritionist.Id, patient.Id, DateTime.UtcNow, read: true);
        await SeedAlertAsync(nutritionist.Id, otherPatient.Id, DateTime.UtcNow);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .DeleteAsync("/api/v1/patients/me?confirmedActivePilotAcknowledged=false");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var remaining = await AlertsOfAsync(nutritionist.Id);
        remaining.Should().ContainSingle().Which.PatientId.Should().Be(otherPatient.Id);
    }

    // ----- Apoyo -----

    private async Task<JsonElement> ListAsync((Guid Id, string KeycloakId, string Email) nutritionist, string query = "")
    {
        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email).GetAsync(ListUrl + query);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Guid> SeedAlertAsync(Guid nutritionistId, Guid patientId, DateTime occurredAt, bool read = false)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var alert = NutritionistAlert.Record(nutritionistId, patientId, NutritionistAlertType.PatientLinked, occurredAt);
        if (read)
        {
            alert.MarkAsRead(occurredAt.AddMinutes(1));
        }

        db.NutritionistAlerts.Add(alert);
        await db.SaveChangesAsync();
        return alert.Id;
    }

    private async Task<IReadOnlyList<NutritionistAlert>> AlertsOfAsync(Guid nutritionistId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.NutritionistAlerts.AsNoTracking().Where(a => a.NutritionistId == nutritionistId).ToListAsync();
    }

    private async Task<string> SeedInvitationAsync(Guid nutritionistId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var code = "INV" + new string(Guid.NewGuid().ToString("N").ToUpperInvariant().Where(char.IsLetterOrDigit).Take(7).ToArray());
        db.InvitationCodes.Add(InvitationCode.Generate(Guid.NewGuid(), code, nutritionistId, DateTime.UtcNow, InvitationCode.Validity));
        await db.SaveChangesAsync();
        return code;
    }

    private async Task<Guid> PublishEventAsync(Guid aggregateId, Domain.Common.IDomainEvent domainEvent)
    {
        using var scope = Factory.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        await writer.PublishAsync(aggregateId, nameof(User), domainEvent);
        await unitOfWork.SaveChangesAsync();
        return await db.OutboxMessages.Where(m => m.AggregateId == aggregateId)
            .OrderByDescending(m => m.OccurredAt).Select(m => m.Id).FirstAsync();
    }

    private async Task ProcessOnceAsync(Guid messageId)
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>().ProcessAsync(messageId);
    }

    private async Task ProcessAllAsync(Type eventType)
    {
        List<Guid> ids;
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            ids = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.EventType == eventType.FullName && m.ProcessedAt == null)
                .Select(m => m.Id)
                .ToListAsync();
        }

        foreach (var id in ids)
        {
            await ProcessOnceAsync(id);
        }
    }
}
