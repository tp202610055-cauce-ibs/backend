using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de <c>POST /glossary/term-requests</c>: el nutricionista pide agregar un término que no está
/// en el glosario; el paciente no puede.
/// </summary>
[Trait("Category", "Integration")]
public sealed class GlossaryTermRequestApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string Url = "/api/v1/glossary/term-requests";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public GlossaryTermRequestApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task SeedGlossaryTermAsync(string term)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        if (!await db.GlossaryTerms.AnyAsync(t => t.Term == term))
        {
            db.GlossaryTerms.Add(GlossaryTerm.SeedEntry(
                Guid.NewGuid(), term, "Definición para pacientes.", "Definición clínica.", GlossaryCategory.ClinicalIbs, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
    }

    private async Task<IReadOnlyList<GlossaryTermRequest>> RequestsAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.GlossaryTermRequests.AsNoTracking().ToListAsync();
    }

    [SkippableFact]
    public async Task Request_NewTerm_Returns201StoresItPendingAndAudits()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email)
            .PostAsJsonAsync(Url, new { term = "  disacaridasas " });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("term").GetString().Should().Be("disacaridasas");
        body.GetProperty("status").GetString().Should().Be("Pending");
        var stored = (await RequestsAsync()).Should().ContainSingle().Subject;
        stored.NutritionistId.Should().Be(nutritionist.Id);
        stored.Term.Should().Be("disacaridasas");
        stored.Status.Should().Be(GlossaryTermRequestStatus.Pending);
        stored.RequestedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        var audit = (await AuditLogsAsync(nameof(GlossaryTermRequest), stored.Id, AuditActionType.Create)).Should().ContainSingle().Subject;
        audit.ActorUserId.Should().Be(nutritionist.Id);
        ContextValue(audit, "term").Should().Be("disacaridasas");
    }

    [SkippableFact]
    public async Task Request_SamePendingTermTwice_ReturnsTheExistingOneWithoutDuplicating()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        var client = NutritionistClient(nutritionist.KeycloakId, nutritionist.Email);

        var first = await client.PostAsJsonAsync(Url, new { term = "disacaridasas" });
        var second = await client.PostAsJsonAsync(Url, new { term = "DISACARIDÁSAS" });

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid().Should().Be(firstId);
        (await RequestsAsync()).Should().ContainSingle();
    }

    [SkippableFact]
    public async Task Request_SameTermByAnotherNutritionist_IsStoredSeparately()
    {
        SkipIfUnavailable();
        var first = await SeedNutritionistAsync();
        var second = await SeedNutritionistAsync();

        await NutritionistClient(first.KeycloakId, first.Email).PostAsJsonAsync(Url, new { term = "disacaridasas" });
        var response = await NutritionistClient(second.KeycloakId, second.Email).PostAsJsonAsync(Url, new { term = "disacaridasas" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await RequestsAsync()).Should().HaveCount(2);
    }

    [SkippableTheory]
    [InlineData("Síndrome de intestino irritable")]
    [InlineData("sindrome de INTESTINO irritable")]
    public async Task Request_TermAlreadyInTheGlossary_Returns409(string term)
    {
        SkipIfUnavailable();
        await SeedGlossaryTermAsync("Síndrome de intestino irritable");
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email).PostAsJsonAsync(Url, new { term });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()
            .Should().Be("glossary_term_already_exists");
        (await RequestsAsync()).Should().BeEmpty();
    }

    [SkippableTheory]
    [InlineData("")]
    [InlineData("    ")]
    public async Task Request_BlankTerm_Returns400(string term)
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email).PostAsJsonAsync(Url, new { term });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [SkippableFact]
    public async Task Request_MissingTerm_Returns400()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email).PostAsJsonAsync(Url, new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Request_TermTooLong_Returns400()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email)
            .PostAsJsonAsync(Url, new { term = new string('a', GlossaryTermRequest.MaxTermLength + 1) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [SkippableFact]
    public async Task Request_AsPatient_Returns403()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email).PostAsJsonAsync(Url, new { term = "disacaridasas" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RequestsAsync()).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task Request_WithoutToken_Returns401()
    {
        SkipIfUnavailable();

        var response = await Factory.CreateClient().PostAsJsonAsync(Url, new { term = "disacaridasas" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
