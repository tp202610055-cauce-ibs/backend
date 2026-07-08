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
using Cauce.Domain.Recommendations;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Recommendations;

/// <summary>
/// Pruebas de integración del motor ONNX end-to-end (TS07). Con <c>Recommendations:EngineKind = "Onnx"</c>,
/// el seeder registra la versión dummy como activa y la generación produce una recomendación trazable al
/// modelo <c>dummy-v0.0.1</c>. Requieren Docker (PostgreSQL + Redis) y el archivo del modelo.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OnnxEngineApiTests : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly RedisFixture _redis;
    private OllamaWireMockFixture _ollama = null!;
    private CustomWebApplicationFactory _factory = null!;

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public OnnxEngineApiTests(PostgresFixture postgres, RedisFixture redis)
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
        _ollama.StubSuccess("Se sugiere ajustar el consumo de algunos alimentos según el perfil FODMAP del paciente.");
        _factory = new CustomWebApplicationFactory(
            _postgres.ConnectionString, _redis.ConnectionString, _ollama.Endpoint, engineKind: "Onnx");

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

    [SkippableFact]
    public async Task Generate_WithOnnxEngine_ProducesRecommendationTraceableToDummyModel()
    {
        Skip.IfNot(_postgres.IsAvailable && _redis.IsAvailable, "Docker (PostgreSQL + Redis) no disponible; se omite.");

        var patient = await SeedPatientWithHistoryAsync();

        var response = await PostWithKey(PatientClient(patient.KeycloakId), "/api/v1/recommendations");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var recommendationId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recommendationId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        var recommendation = await db.Recommendations.AsNoTracking().Include(r => r.Items).FirstAsync(r => r.Id == recommendationId);
        recommendation.Items.Should().NotBeEmpty();
        recommendation.ModelVersionId.Should().NotBeNull();

        var modelVersion = await db.Set<ModelVersion>().AsNoTracking().FirstAsync(m => m.Id == recommendation.ModelVersionId);
        modelVersion.VersionName.Should().Be("dummy-v0.0.1");
        modelVersion.IsDummy.Should().BeTrue();
    }

    private async Task<(Guid Id, string KeycloakId)> SeedPatientWithHistoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Patient).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId, $"user-{Guid.NewGuid():N}@cauce.local", "Paciente", roleId);
        db.Users.Add(user);

        var dob = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30);
        db.PatientProfiles.Add(PatientProfile.Create(
            Guid.NewGuid(), user.Id, dob, BiologicalSex.Male, 70m, 175m, IbsSubtype.IbsM, null, null, DateTime.UtcNow));

        var foodIds = await db.FoodItems.AsNoTracking().OrderBy(f => f.Name).Select(f => f.Id).Take(6).ToListAsync();
        var timestamp = DateTime.UtcNow.AddDays(-1);
        foreach (var foodId in foodIds)
        {
            db.Meals.Add(Meal.Register(
                Guid.NewGuid(), Guid.NewGuid(), user.Id, MealTime.Lunch, timestamp, timestamp,
                new[] { new MealItemInput(foodId, null, 100m, MeasurementUnit.Grams) }, DateTime.UtcNow));
        }

        await db.SaveChangesAsync();
        return (user.Id, keycloakId);
    }

    private HttpClient PatientClient(string keycloakId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtBuilder.Build(keycloakId, "patient@cauce.local", UserRoles.Patient));
        return client;
    }

    private static async Task<HttpResponseMessage> PostWithKey(HttpClient client, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
