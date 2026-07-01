using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Recommendations.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Recommendations;

/// <summary>
/// Pruebas de integración end-to-end del módulo de recomendaciones. Requieren Docker
/// (PostgreSQL + Redis) y usan WireMock como doble de Ollama; se omiten si Docker no está disponible.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RecommendationsApiTests
    : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IAsyncLifetime
{
    private const string ValidExplanation =
        "Se sugiere ajustar el consumo de algunos alimentos para el bienestar del paciente, dado su perfil " +
        "FODMAP. Estos cambios pueden asociarse a una mejor tolerancia digestiva con el tiempo.";

    private readonly PostgresFixture _postgres;
    private readonly RedisFixture _redis;
    private OllamaWireMockFixture _ollama = null!;
    private CustomWebApplicationFactory _factory = null!;

    /// <summary>
    /// Inicializa la prueba con los fixtures de PostgreSQL y Redis.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis.</param>
    public RecommendationsApiTests(PostgresFixture postgres, RedisFixture redis)
    {
        _postgres = postgres;
        _redis = redis;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (!_postgres.IsAvailable || !_redis.IsAvailable)
        {
            return;
        }

        _ollama = new OllamaWireMockFixture();
        _ollama.StubSuccess(ValidExplanation);
        _factory = new CustomWebApplicationFactory(_postgres.ConnectionString, _redis.ConnectionString, _ollama.Endpoint);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<UserRolesSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<FoodItemsSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<RecommendationsModelVersionsSeeder>().SeedAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        _ollama?.Dispose();
    }

    // ----- Migración (DEC-B4-08) -----

    [SkippableFact]
    public async Task Migration_AddsFodmapGranularColumnsToFoodItems()
    {
        SkipIfUnavailable();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        var food = await db.FoodItems.AsNoTracking().FirstAsync();

        food.OligosLevel.Should().Be(0);
        food.LactoseLevel.Should().Be(0);
    }

    [SkippableFact]
    public async Task Migration_RespectsCheckConstraints_RejectsValueAbove2()
    {
        SkipIfUnavailable();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        var act = () => db.Database.ExecuteSqlRawAsync("UPDATE food_items SET oligos_level = 3");

        await act.Should().ThrowAsync<Exception>();
    }

    [SkippableFact]
    public async Task Migration_PreservesExistingFoodItemsRows()
    {
        SkipIfUnavailable();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        (await db.FoodItems.AsNoTracking().CountAsync()).Should().Be(FoodItemsSeeder.CatalogSize);
    }

    // ----- Seeder (DEC-B4-10) -----

    [SkippableFact]
    public async Task Seeder_InsertsRuleVersionAsActive()
    {
        SkipIfUnavailable();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        var version = await db.ModelVersions.AsNoTracking().SingleOrDefaultAsync(v => v.VersionName == "rule-v1.0.0");

        version.Should().NotBeNull();
        version!.IsActive.Should().BeTrue();
    }

    [SkippableFact]
    public async Task Seeder_IsIdempotent_DoesNotInsertTwice()
    {
        SkipIfUnavailable();
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RecommendationsModelVersionsSeeder>().SeedAsync();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        (await db.ModelVersions.AsNoTracking().CountAsync(v => v.VersionName == "rule-v1.0.0")).Should().Be(1);
    }

    // ----- Autorización -----

    [SkippableFact]
    public async Task ListMine_Unauthenticated_Returns401()
    {
        SkipIfUnavailable();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/recommendations/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task GetById_OtherPatient_Returns403()
    {
        SkipIfUnavailable();
        var owner = await SeedPatientWithHistoryAsync();
        var recommendationId = await GenerateAsync(owner.KeycloakId);
        var intruder = await SeedPatientAsync();

        var response = await PatientClient(intruder.KeycloakId).GetAsync($"/api/v1/recommendations/{recommendationId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task Approve_NutritionistNotAssigned_Returns403()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientWithHistoryAsync();
        var recommendationId = await GenerateAsync(patient.KeycloakId);
        var nutritionist = await SeedNutritionistAsync();

        var response = await Approve(nutritionist.KeycloakId, recommendationId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----- Generación -----

    [SkippableFact]
    public async Task Generate_WithInsufficientHistory_Returns422()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PostWithKey(PatientClient(patient.KeycloakId), "/api/v1/recommendations", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [SkippableFact]
    public async Task Generate_WithOllamaDown_FallsBackSuccessfully()
    {
        SkipIfUnavailable();
        _ollama.Reset();
        _ollama.StubFailure(500);
        var patient = await SeedPatientWithHistoryAsync();

        var recommendationId = await GenerateAsync(patient.KeycloakId);
        var detail = await GetDetail(patient.KeycloakId, recommendationId);

        detail.GetProperty("explanationSource").GetString().Should().Be("Fallback");
    }

    [SkippableFact]
    public async Task Generate_IdempotentReplay_ReturnsCachedResult()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientWithHistoryAsync();
        var client = PatientClient(patient.KeycloakId);
        var key = Guid.NewGuid();

        var first = await PostWithKey(client, "/api/v1/recommendations", content: null, key);
        var second = await PostWithKey(client, "/api/v1/recommendations", content: null, key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recommendationId").GetGuid();
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recommendationId").GetGuid();
        secondId.Should().Be(firstId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        (await db.Recommendations.AsNoTracking().CountAsync(r => r.PatientId == patient.Id)).Should().Be(1);
    }

    [SkippableFact]
    public async Task FullFlow_Generate_Approve_Deliver_Feedback()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientWithHistoryAsync();
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var recommendationId = await GenerateAsync(patient.KeycloakId);
        var initial = await GetDetail(patient.KeycloakId, recommendationId);
        initial.GetProperty("status").GetString().Should().Be("PendingReview");
        initial.GetProperty("explanationSource").GetString().Should().Be("LlmGenerated");

        var pending = await NutritionistClient(nutritionist.KeycloakId).GetAsync("/api/v1/recommendations/pending-review");
        pending.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Approve(nutritionist.KeycloakId, recommendationId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetDetail(patient.KeycloakId, recommendationId)).GetProperty("status").GetString().Should().Be("Approved");

        (await Deliver(patient.KeycloakId, recommendationId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetDetail(patient.KeycloakId, recommendationId)).GetProperty("status").GetString().Should().Be("Delivered");

        (await SubmitFeedback(patient.KeycloakId, recommendationId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetDetail(patient.KeycloakId, recommendationId)).GetProperty("status").GetString().Should().Be("FeedbackReceived");
    }

    [SkippableFact]
    public async Task Generate_WithOllamaUnsafeOutput_FallsBack()
    {
        SkipIfUnavailable();
        _ollama.Reset();
        _ollama.StubSuccess("Este alimento te causa síntomas y sin duda es una enfermedad que debes curar de inmediato.");
        var patient = await SeedPatientWithHistoryAsync();

        var recommendationId = await GenerateAsync(patient.KeycloakId);
        var detail = await GetDetail(patient.KeycloakId, recommendationId);

        detail.GetProperty("explanationSource").GetString().Should().Be("Fallback");
    }

    [SkippableFact]
    public async Task SubmitFeedback_SameKeyDifferentPayload_Returns409()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientWithHistoryAsync();
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);
        var recommendationId = await GenerateAsync(patient.KeycloakId);
        await Approve(nutritionist.KeycloakId, recommendationId);
        await Deliver(patient.KeycloakId, recommendationId);

        var client = PatientClient(patient.KeycloakId);
        var key = Guid.NewGuid();
        var first = await PostWithKey(client, $"/api/v1/recommendations/{recommendationId}/feedback",
            new { wasApplied = true, outcome = "Improvement", comment = "bien" }, key);
        var conflicting = await PostWithKey(client, $"/api/v1/recommendations/{recommendationId}/feedback",
            new { wasApplied = false, outcome = "Worsening", comment = "distinto" }, key);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        conflicting.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ----- Helpers -----

    private void SkipIfUnavailable()
    {
        Skip.IfNot(_postgres.IsAvailable && _redis.IsAvailable, "Docker (PostgreSQL + Redis) no disponible; se omite.");
    }

    private async Task<Guid> GenerateAsync(string patientKeycloakId)
    {
        var response = await PostWithKey(PatientClient(patientKeycloakId), "/api/v1/recommendations", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recommendationId").GetGuid();
    }

    private async Task<JsonElement> GetDetail(string patientKeycloakId, Guid recommendationId)
    {
        var response = await PatientClient(patientKeycloakId).GetAsync($"/api/v1/recommendations/{recommendationId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> Approve(string nutritionistKeycloakId, Guid recommendationId) =>
        PostWithKey(NutritionistClient(nutritionistKeycloakId), $"/api/v1/recommendations/{recommendationId}/approve",
            new { note = "Recomendación revisada y aprobada por el nutricionista." });

    private Task<HttpResponseMessage> Deliver(string patientKeycloakId, Guid recommendationId) =>
        PostWithKey(PatientClient(patientKeycloakId), $"/api/v1/recommendations/{recommendationId}/deliver", content: null);

    private Task<HttpResponseMessage> SubmitFeedback(string patientKeycloakId, Guid recommendationId) =>
        PostWithKey(PatientClient(patientKeycloakId), $"/api/v1/recommendations/{recommendationId}/feedback",
            new { wasApplied = true, outcome = "Improvement", comment = "Me sentí mejor." });

    private static async Task<HttpResponseMessage> PostWithKey(HttpClient client, string url, object? content, Guid? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return await client.SendAsync(request);
    }

    private HttpClient PatientClient(string keycloakId) => AuthedClient(keycloakId, "patient@cauce.local", UserRoles.Patient);

    private HttpClient NutritionistClient(string keycloakId) => AuthedClient(keycloakId, "nutri@cauce.local", UserRoles.Nutritionist);

    private HttpClient AuthedClient(string keycloakId, string email, string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtBuilder.Build(keycloakId, email, role));
        return client;
    }

    private async Task<(Guid Id, string KeycloakId)> SeedPatientAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Patient).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId, $"user-{Guid.NewGuid():N}@cauce.local", "Paciente", roleId);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, keycloakId);
    }

    private async Task<(Guid Id, string KeycloakId)> SeedNutritionistAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Nutritionist).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var user = User.CreateNutritionist(Guid.NewGuid(), keycloakId, $"nutri-{Guid.NewGuid():N}@cauce.local", "Nutri", roleId);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, keycloakId);
    }

    private async Task SeedPatientProfileAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var dob = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30);
        var profile = PatientProfile.Create(Guid.NewGuid(), userId, dob, BiologicalSex.Male, 70m, 175m, IbsSubtype.IbsM, null, null, DateTime.UtcNow);
        db.PatientProfiles.Add(profile);
        await db.SaveChangesAsync();
    }

    private async Task<(Guid Id, string KeycloakId)> SeedPatientWithHistoryAsync()
    {
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var foodIds = await db.FoodItems.AsNoTracking().OrderBy(f => f.Name).Select(f => f.Id).Take(6).ToListAsync();
        var timestamp = DateTime.UtcNow.AddDays(-1);
        foreach (var foodId in foodIds)
        {
            var meal = Meal.Register(
                Guid.NewGuid(), Guid.NewGuid(), patient.Id, MealTime.Lunch, timestamp, timestamp,
                new[] { new MealItemInput(foodId, null, 100m, MeasurementUnit.Grams) }, DateTime.UtcNow);
            db.Meals.Add(meal);
        }

        await db.SaveChangesAsync();
        return patient;
    }

    private async Task AssignAsync(Guid nutritionistId, Guid patientId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        db.NutritionistPatients.Add(NutritionistPatient.Establish(Guid.NewGuid(), nutritionistId, patientId, null, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }
}
