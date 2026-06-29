using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración end-to-end del módulo de registro clínico. Requieren Docker
/// (PostgreSQL + Redis); se omiten si no está disponible.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ClinicalRegistryApiTests : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly RedisFixture _redis;
    private CustomWebApplicationFactory _factory = null!;

    /// <summary>
    /// Inicializa la prueba con los fixtures de PostgreSQL y Redis.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis.</param>
    public ClinicalRegistryApiTests(PostgresFixture postgres, RedisFixture redis)
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

        _factory = new CustomWebApplicationFactory(_postgres.ConnectionString, _redis.ConnectionString);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<UserRolesSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<FoodItemsSeeder>().SeedAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    // ----- Comidas e idempotencia -----

    [SkippableFact]
    public async Task CreateMeal_AsPatient_Returns201AndPersists()
    {
        SkipIfUnavailable();
        var (userId, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();
        var clientGuid = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/v1/meals", MealBody(clientGuid, foodId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        (await db.Meals.AsNoTracking().AnyAsync(m => m.PatientId == userId && m.ClientGuid == clientGuid)).Should().BeTrue();
    }

    [SkippableFact]
    public async Task CreateMeal_DuplicateClientGuid_Returns200AndDoesNotCreateSecondRow()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();
        var clientGuid = Guid.NewGuid();
        var body = MealBody(clientGuid, foodId);

        var first = await client.PostAsJsonAsync("/api/v1/meals", body);
        var second = await client.PostAsJsonAsync("/api/v1/meals", body);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        (await db.Meals.AsNoTracking().CountAsync(m => m.ClientGuid == clientGuid)).Should().Be(1);
    }

    [SkippableFact]
    public async Task CreateMeal_SameClientGuidDifferentPayload_Returns409()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();
        var clientGuid = Guid.NewGuid();

        var first = await client.PostAsJsonAsync("/api/v1/meals", MealBody(clientGuid, foodId, quantity: 100m));
        var conflicting = await client.PostAsJsonAsync("/api/v1/meals", MealBody(clientGuid, foodId, quantity: 250m));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        conflicting.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [SkippableFact]
    public async Task CreateMeal_WithUnknownFoodItem_Returns404()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);

        var response = await client.PostAsJsonAsync("/api/v1/meals", MealBody(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----- Síntomas y correlación 4h -----

    [SkippableFact]
    public async Task CreateSymptom_WithMealInWindow_AssociatesMeal()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();
        var reference = DateTime.UtcNow;
        await client.PostAsJsonAsync("/api/v1/meals", MealBody(Guid.NewGuid(), foodId, clientCreatedAt: reference.AddHours(-2)));

        var response = await client.PostAsJsonAsync("/api/v1/symptoms", SymptomBody(Guid.NewGuid(), reference));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("hasMealAssociation").GetBoolean().Should().BeTrue();
    }

    [SkippableFact]
    public async Task CreateSymptom_WithMealOutsideWindow_DoesNotAssociate()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();
        var reference = DateTime.UtcNow;
        await client.PostAsJsonAsync("/api/v1/meals", MealBody(Guid.NewGuid(), foodId, clientCreatedAt: reference.AddHours(-5)));

        var response = await client.PostAsJsonAsync("/api/v1/symptoms", SymptomBody(Guid.NewGuid(), reference));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("hasMealAssociation").GetBoolean().Should().BeFalse();
    }

    // ----- Alimentos personalizados -----

    [SkippableFact]
    public async Task CreateCustomFood_Happy_Returns201()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();

        var response = await client.PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody("Mi plato", foodId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [SkippableFact]
    public async Task CreateCustomFood_DuplicateName_Returns409()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();
        await client.PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody("Plato repetido", foodId));

        var duplicate = await client.PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody("Plato repetido", foodId));

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [SkippableFact]
    public async Task DeleteCustomFood_NotReferenced_Returns204()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();
        var created = await client.PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody("Eliminable", foodId));
        var customFoodId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("customFoodId").GetGuid();

        var response = await client.DeleteAsync($"/api/v1/custom-foods/{customFoodId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [SkippableFact]
    public async Task DeleteCustomFood_ReferencedByMeal_Returns409()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();
        var created = await client.PostAsJsonAsync("/api/v1/custom-foods", CustomFoodBody("EnUso", foodId));
        var customFoodId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("customFoodId").GetGuid();
        await client.PostAsJsonAsync("/api/v1/meals", MealBodyWithCustomFood(Guid.NewGuid(), customFoodId));

        var response = await client.DeleteAsync($"/api/v1/custom-foods/{customFoodId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ----- Notas clínicas -----

    [SkippableFact]
    public async Task CreateClinicalNote_WithBothAssociations_Returns400()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/clinical-notes", new { mealId = Guid.NewGuid(), symptomId = Guid.NewGuid(), content = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ----- IBS-SSS -----

    [SkippableFact]
    public async Task CreateIbsSssBaseline_Happy_TriggersOnboardingAndReturns201()
    {
        SkipIfUnavailable();
        var (userId, keycloakId) = await SeedPatientAsync();
        await SeedPatientProfileAsync(userId);
        var client = PatientClient(keycloakId);

        var response = await client.PostAsJsonAsync("/api/v1/ibs-sss", IbsSssBody("Baseline"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("triggeredOnboardingCompletion").GetBoolean().Should().BeTrue();
        json.GetProperty("totalScore").GetInt32().Should().Be(250);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        (await db.PatientProfiles.AsNoTracking().AnyAsync(p => p.UserId == userId && p.OnboardingCompleted)).Should().BeTrue();
    }

    [SkippableFact]
    public async Task CreateIbsSssBaseline_WhenBaselineExists_Returns409()
    {
        SkipIfUnavailable();
        var (userId, keycloakId) = await SeedPatientAsync();
        await SeedPatientProfileAsync(userId);
        var client = PatientClient(keycloakId);
        await client.PostAsJsonAsync("/api/v1/ibs-sss", IbsSssBody("Baseline"));

        var second = await client.PostAsJsonAsync("/api/v1/ibs-sss", IbsSssBody("Baseline"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ----- Sincronización por lotes -----

    [SkippableFact]
    public async Task SyncBatch_WithDuplicate_ReturnsAcceptedAndDuplicates()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();

        // Para que el reintento sea un duplicado idéntico (no un mismatch), la carga del
        // lote debe ser exactamente igual a la pre-creada: mismo client_guid y mismos campos.
        var duplicateMeal = BatchMealAt(Guid.NewGuid(), foodId, DateTime.UtcNow);
        (await client.PostAsJsonAsync("/api/v1/meals", duplicateMeal)).StatusCode.Should().Be(HttpStatusCode.Created);

        var batch = new
        {
            meals = new object[]
            {
                AsBatchMeal(Guid.NewGuid(), foodId),
                AsBatchMeal(Guid.NewGuid(), foodId),
                duplicateMeal
            },
            symptoms = new[] { AsBatchSymptom(Guid.NewGuid()), AsBatchSymptom(Guid.NewGuid()) }
        };

        var response = await client.PostAsJsonAsync("/api/v1/sync/batch", batch);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("accepted").GetArrayLength().Should().Be(4);
        json.GetProperty("duplicates").GetArrayLength().Should().Be(1);
    }

    [SkippableFact]
    public async Task SyncBatch_ExceedsLimit_Returns400()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);
        var foodId = await FirstFoodIdAsync();
        var meals = Enumerable.Range(0, 201).Select(_ => AsBatchMeal(Guid.NewGuid(), foodId)).ToArray();

        var response = await client.PostAsJsonAsync("/api/v1/sync/batch", new { meals, symptoms = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ----- Catálogo -----

    [SkippableFact]
    public async Task ListFoodItemsCatalog_Returns200WithSeededCount()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);

        var response = await client.GetAsync($"/api/v1/foods?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("totalCount").GetInt32().Should().Be(FoodItemsSeeder.CatalogSize);
    }

    [SkippableFact]
    public async Task SearchFoodItems_ByName_ReturnsMatch()
    {
        SkipIfUnavailable();
        var (_, keycloakId) = await SeedPatientAsync();
        var client = PatientClient(keycloakId);

        var response = await client.GetAsync("/api/v1/foods/search?q=quinua");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetArrayLength().Should().BeGreaterThan(0);
    }

    // ----- Helpers -----

    private void SkipIfUnavailable()
    {
        Skip.IfNot(_postgres.IsAvailable && _redis.IsAvailable, "Docker (PostgreSQL + Redis) no disponible; se omite.");
    }

    private static object MealBody(Guid clientGuid, Guid foodId, decimal quantity = 100m, DateTime? clientCreatedAt = null)
    {
        var timestamp = clientCreatedAt ?? DateTime.UtcNow;
        return new
        {
            clientGuid,
            mealTime = "Lunch",
            consumedAt = timestamp,
            clientCreatedAt = timestamp,
            items = new[] { new { foodId, customFoodId = (Guid?)null, quantity, unit = "Grams" } }
        };
    }

    private static object MealBodyWithCustomFood(Guid clientGuid, Guid customFoodId)
    {
        var timestamp = DateTime.UtcNow;
        return new
        {
            clientGuid,
            mealTime = "Dinner",
            consumedAt = timestamp,
            clientCreatedAt = timestamp,
            items = new[] { new { foodId = (Guid?)null, customFoodId, quantity = 1m, unit = "Units" } }
        };
    }

    private static object SymptomBody(Guid clientGuid, DateTime reference) => new
    {
        clientGuid,
        symptomType = "Bloating",
        intensity = 60,
        occurredAt = reference,
        clientCreatedAt = reference
    };

    private static object CustomFoodBody(string name, Guid foodId) => new
    {
        name,
        portionSizeGrams = 200m,
        ingredients = new[] { new { foodId, proportionGrams = 100m } }
    };

    private static object IbsSssBody(string assessmentType) => new
    {
        assessmentType,
        painSeverity = 50,
        painFrequency = 50,
        bloatingSeverity = 50,
        bowelHabitsDissatisfaction = 50,
        lifeInterference = 50
    };

    private static object AsBatchMeal(Guid clientGuid, Guid foodId) => BatchMealAt(clientGuid, foodId, DateTime.UtcNow);

    private static object BatchMealAt(Guid clientGuid, Guid foodId, DateTime timestamp) => new
    {
        clientGuid,
        mealTime = "Breakfast",
        consumedAt = timestamp,
        clientCreatedAt = timestamp,
        items = new[] { new { foodId, customFoodId = (Guid?)null, quantity = 50m, unit = "Grams" } }
    };

    private static object AsBatchSymptom(Guid clientGuid)
    {
        var timestamp = DateTime.UtcNow;
        return new
        {
            clientGuid,
            symptomType = "Flatulence",
            intensity = 30,
            occurredAt = timestamp,
            clientCreatedAt = timestamp
        };
    }

    private HttpClient PatientClient(string keycloakId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtBuilder.Build(keycloakId, "patient@cauce.local", UserRoles.Patient));
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

    private async Task SeedPatientProfileAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var dob = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30);
        var profile = PatientProfile.Create(Guid.NewGuid(), userId, dob, BiologicalSex.Male, 70m, 175m, IbsSubtype.IbsM, null, null, DateTime.UtcNow);
        db.PatientProfiles.Add(profile);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> FirstFoodIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        return await db.FoodItems.AsNoTracking().OrderBy(f => f.Name).Select(f => f.Id).FirstAsync();
    }
}
