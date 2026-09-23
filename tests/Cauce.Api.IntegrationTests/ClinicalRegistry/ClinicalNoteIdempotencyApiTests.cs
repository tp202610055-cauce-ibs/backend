using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración de la idempotencia de las notas clínicas (DEC-B3-04). Van por HTTP para
/// atravesar el pipeline real de MediatR: lo que se verifica es que el <c>IdempotencyBehavior</c>
/// esté efectivamente enganchado para este comando, cosa que una prueba que arme el behavior a mano
/// no puede demostrar.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ClinicalNoteIdempotencyApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public ClinicalNoteIdempotencyApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task<Guid> SeedSymptomAsync(Guid patientId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var now = DateTime.UtcNow;
        var symptom = Cauce.Domain.ClinicalRegistry.Symptom.Report(
            Guid.NewGuid(), Guid.NewGuid(), patientId, SymptomType.Bloating, 40, now, now, now);
        db.Symptoms.Add(symptom);
        await db.SaveChangesAsync();
        return symptom.Id;
    }

    private async Task<int> NoteCountAsync(Guid patientId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.ClinicalNotes.AsNoTracking().CountAsync(note => note.PatientId == patientId);
    }

    [SkippableFact]
    public async Task Create_SameIdempotencyKeyTwice_ReturnsTheSameNoteWithoutDuplicating()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var symptomId = await SeedSymptomAsync(patient.Id);
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var key = Guid.NewGuid();
        var body = new { symptomId, content = "Distensión leve después de la cena." };

        var first = await PostWithKey(client, "/api/v1/clinical-notes", body, key);
        var second = await PostWithKey(client, "/api/v1/clinical-notes", body, key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK, "un reintento idempotente es una repetición, no una creación");

        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("noteId").GetGuid();
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("noteId").GetGuid();
        secondId.Should().Be(firstId);

        (await NoteCountAsync(patient.Id)).Should().Be(1);
    }

    [SkippableFact]
    public async Task Create_SameKeyWithDifferentContent_Returns409()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var symptomId = await SeedSymptomAsync(patient.Id);
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var key = Guid.NewGuid();

        await PostWithKey(client, "/api/v1/clinical-notes", new { symptomId, content = "Primera nota." }, key);
        var conflict = await PostWithKey(client, "/api/v1/clinical-notes", new { symptomId, content = "Nota distinta." }, key);

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("idempotency_mismatch");

        (await NoteCountAsync(patient.Id)).Should().Be(1);
    }

    [SkippableFact]
    public async Task Create_ClientGuidInBody_IsAccepted()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var symptomId = await SeedSymptomAsync(patient.Id);
        var clientGuid = Guid.NewGuid();

        var response = await PatientClient(patient.KeycloakId, patient.Email).PostAsJsonAsync(
            "/api/v1/clinical-notes",
            new { symptomId, content = "Nota con client_guid en el cuerpo.", clientGuid });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var stored = await db.ClinicalNotes.AsNoTracking().SingleAsync(note => note.PatientId == patient.Id);
        stored.ClientGuid.Should().Be(clientGuid);
    }

    [SkippableFact]
    public async Task Create_HeaderAndBodyDisagree_Returns400()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var symptomId = await SeedSymptomAsync(patient.Id);
        var client = PatientClient(patient.KeycloakId, patient.Email);

        var response = await PostWithKey(
            client,
            "/api/v1/clinical-notes",
            new { symptomId, content = "Nota inconsistente.", clientGuid = Guid.NewGuid() },
            Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await NoteCountAsync(patient.Id)).Should().Be(0);
    }

    [SkippableFact]
    public async Task Create_WithoutAnyIdempotencyKey_Returns400()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var symptomId = await SeedSymptomAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email).PostAsJsonAsync(
            "/api/v1/clinical-notes",
            new { symptomId, content = "Nota sin clave." });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetProperty("clientGuid").EnumerateArray().Should().NotBeEmpty();
    }

    [SkippableFact]
    public async Task List_ReturnsTheClientGuidSoTheDeviceCanReconcile()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var symptomId = await SeedSymptomAsync(patient.Id);
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var clientGuid = Guid.NewGuid();

        await PostWithKey(client, "/api/v1/clinical-notes", new { symptomId, content = "Nota listada." }, clientGuid);

        var from = DateTime.UtcNow.AddDays(-1).ToString("O");
        var to = DateTime.UtcNow.AddDays(1).ToString("O");
        var response = await client.GetAsync($"/api/v1/clinical-notes?from={from}&to={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notes = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        notes.Should().ContainSingle();
        notes[0].GetProperty("clientGuid").GetGuid().Should().Be(clientGuid);
    }
}
