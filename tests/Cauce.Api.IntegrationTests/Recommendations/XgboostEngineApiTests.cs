using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.Commands;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Api.IntegrationTests.Recommendations.Support;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Seeders;
using Cauce.Infrastructure.Recommendations.Engines;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cauce.Api.IntegrationTests.Recommendations;

/// <summary>
/// Pruebas de integración del motor XGBoost: cambio de motor con seed-catalogs, generación trazable al modelo
/// real y fallos de arranque. Cada prueba parte con la regla activa. Requieren Docker.
/// </summary>
[Trait("Category", "Integration")]
public sealed class XgboostEngineApiTests
    : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IClassFixture<MinioFixture>, IAsyncLifetime
{
    private const string ModelVersionName = "xgboost-sii-v1.2.0";
    private const string RuleVersionName = "rule-v1.0.0";
    private const string ModelSha256 = "eb9da0167ed7dc4f9db2d0f5e0da2b3e9eed0e250f993077c05fc378013f7481";

    private readonly PostgresFixture _postgres;
    private readonly RedisFixture _redis;
    private readonly MinioFixture _minio;
    private OllamaWireMockFixture _ollama = null!;
    private CustomWebApplicationFactory _ruleFactory = null!;

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public XgboostEngineApiTests(PostgresFixture postgres, RedisFixture redis, MinioFixture minio)
    {
        _postgres = postgres;
        _redis = redis;
        _minio = minio;
    }

    private bool Available => _postgres.IsAvailable && _redis.IsAvailable && _minio.IsAvailable;

    private static string ModelPath => Path.Combine(AppContext.BaseDirectory, "TestData", "xgboost-sii-v1.2.0.onnx");

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (!Available)
        {
            return;
        }

        _ollama = new OllamaWireMockFixture();
        _ollama.StubSuccess("Se sugiere ajustar el consumo de algunos alimentos según el perfil FODMAP del paciente.");
        _ruleFactory = CreateFactory();

        using var scope = _ruleFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<UserRolesSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<FoodItemsSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<RecommendationsModelVersionsSeeder>().SeedAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_ruleFactory is not null)
        {
            await _ruleFactory.DisposeAsync();
        }

        _ollama?.Dispose();
    }

    private CustomWebApplicationFactory CreateFactory(
        string? engineKind = null,
        IReadOnlyDictionary<string, string?>? settings = null) => new(
        _postgres.ConnectionString,
        _redis.ConnectionString,
        _ollama.Endpoint,
        minioEndpoint: _minio.Endpoint,
        minioAccessKey: _minio.AccessKey,
        minioSecretKey: _minio.SecretKey,
        engineKind: engineKind,
        additionalSettings: settings);

    private CustomWebApplicationFactory XgboostFactory(string modelPath = "", string sha256 = ModelSha256) => CreateFactory(
        "Xgboost",
        new Dictionary<string, string?>
        {
            ["Recommendations:XgboostModelPath"] = modelPath.Length == 0 ? ModelPath : modelPath,
            ["Recommendations:XgboostModelSha256"] = sha256
        });

    private static XgboostRecommendationEngine CreateEngine() => new(
        Options.Create(new RecommendationsOptions
        {
            EngineKind = "Xgboost",
            XgboostModelPath = ModelPath,
            XgboostModelSha256 = ModelSha256
        }),
        NullLogger<XgboostRecommendationEngine>.Instance);

    // Sin arrancar con EngineKind=Xgboost: la comprobación de arranque exige que el seed ya se haya hecho.
    private WebApplicationFactory<Program> FactoryWithEngine(IRecommendationEngine engine) =>
        _ruleFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRecommendationEngine>();
            services.AddSingleton(engine);
        }));

    private async Task ActivateAsync(IRecommendationEngine engine)
    {
        using var scope = _ruleFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        await new RecommendationsModelVersionsSeeder(db, engine, NullLogger<RecommendationsModelVersionsSeeder>.Instance)
            .SeedAsync();
    }

    private async Task<List<ModelVersion>> ActiveVersionsAsync()
    {
        using var scope = _ruleFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        return await db.ModelVersions.AsNoTracking().Where(version => version.IsActive).ToListAsync();
    }

    [SkippableFact]
    public async Task SeedCatalogs_SwitchingEngines_LeavesOnlyTheConfiguredVersionActive()
    {
        Skip.IfNot(Available, "Docker no disponible; se omite.");
        using var engine = CreateEngine();
        await using var xgboostSeed = FactoryWithEngine(engine);

        var toXgboost = await CommandRunner.RunAsync(xgboostSeed.Services, ["seed-catalogs"]);
        var afterXgboost = await ActiveVersionsAsync();
        var toRule = await CommandRunner.RunAsync(_ruleFactory.Services, ["seed-catalogs"]);
        var afterRule = await ActiveVersionsAsync();
        var backToXgboost = await CommandRunner.RunAsync(xgboostSeed.Services, ["seed-catalogs"]);
        var afterSecondSwitch = await ActiveVersionsAsync();

        new[] { toXgboost, toRule, backToXgboost }.Should().OnlyContain(code => code == CommandRunner.Success);
        afterXgboost.Should().ContainSingle().Which.VersionName.Should().Be(ModelVersionName);
        afterRule.Should().ContainSingle().Which.VersionName.Should().Be(RuleVersionName);
        afterSecondSwitch.Should().ContainSingle().Which.VersionName.Should().Be(ModelVersionName);
    }

    [SkippableFact]
    public async Task Seeder_FirstRegistration_StoresTheModelCard()
    {
        Skip.IfNot(Available, "Docker no disponible; se omite.");
        using var engine = CreateEngine();

        await ActivateAsync(engine);

        var version = (await ActiveVersionsAsync()).Single();
        version.VersionName.Should().Be(ModelVersionName);
        version.VerifyIntegrity(ModelSha256).Should().BeTrue();
        version.IsDummy.Should().BeFalse();
        version.TrainingDatasetSize.Should().Be(175_000);

        using var metrics = JsonDocument.Parse(version.PerformanceMetricsJson);
        var testMetrics = metrics.RootElement.GetProperty("test_metrics");
        testMetrics.GetProperty("accuracy").GetDouble().Should().Be(0.7215);
        testMetrics.GetProperty("precision").GetDouble().Should().Be(0.8112);
        testMetrics.GetProperty("recall").GetDouble().Should().Be(0.5902);
        testMetrics.GetProperty("f1").GetDouble().Should().Be(0.6833);
        testMetrics.GetProperty("auc_roc").GetDouble().Should().Be(0.7674);
    }

    [SkippableFact]
    public async Task Seeder_RuleAlreadyActive_ChangesNothing()
    {
        Skip.IfNot(Available, "Docker no disponible; se omite.");
        var before = (await ActiveVersionsAsync()).Single();

        using var scope = _ruleFactory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RecommendationsModelVersionsSeeder>().SeedAsync();

        var after = (await ActiveVersionsAsync()).Single();
        after.Id.Should().Be(before.Id);
        after.DeployedAt.Should().Be(before.DeployedAt);
        after.PerformanceMetricsJson.Should().Be(before.PerformanceMetricsJson);
    }

    [SkippableFact]
    public async Task Seeder_RegisteredVersionWithAnotherHash_ThrowsAndKeepsTheActiveVersion()
    {
        Skip.IfNot(Available, "Docker no disponible; se omite.");
        using var engine = CreateEngine();
        await ActivateAsync(engine);

        var act = () => ActivateAsync(new FixedDescriptorEngine(new ModelVersionDescriptor(RuleVersionName, "otro-hash", "Rule")));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{RuleVersionName}*SHA-256*");
        (await ActiveVersionsAsync()).Should().ContainSingle().Which.VersionName.Should().Be(ModelVersionName);
    }

    [SkippableFact]
    public async Task Generate_WithXgboostEngine_ScoresWithTheRealModelAndTracesItsVersion()
    {
        Skip.IfNot(Available, "Docker no disponible; se omite.");
        using var engine = CreateEngine();
        await ActivateAsync(engine);
        await using var factory = XgboostFactory();

        var patient = await SeedPatientWithHistoryAsync(factory);
        var nutritionistKeycloakId = await SeedAssignedNutritionistAsync(factory, patient.Id);
        var patientClient = AuthedClient(factory, patient.KeycloakId, UserRoles.Patient);
        var nutritionistClient = AuthedClient(factory, nutritionistKeycloakId, UserRoles.Nutritionist);

        var response = await PostWithKey(patientClient, "/api/v1/recommendations");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var recommendationId = body.GetProperty("recommendationId").GetGuid();
        body.GetProperty("requiresReview").GetBoolean().Should().BeTrue();

        var detailUrl = $"/api/v1/recommendations/{recommendationId}";
        (await patientClient.GetAsync(detailUrl)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var detail = await nutritionistClient.GetFromJsonAsync<JsonElement>(detailUrl);
        detail.GetProperty("modelVersionName").GetString().Should().Be(ModelVersionName);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var recommendation = await db.Recommendations.AsNoTracking()
            .Include(r => r.Items)
            .FirstAsync(r => r.Id == recommendationId);
        var version = await db.ModelVersions.AsNoTracking().FirstAsync(v => v.Id == recommendation.ModelVersionId);
        var foods = await db.FoodItems.AsNoTracking().ToDictionaryAsync(food => food.Name, food => food.Id);

        version.VersionName.Should().Be(ModelVersionName);
        version.IsDummy.Should().BeFalse();

        var byFood = recommendation.Items.ToDictionary(item => item.FoodId);
        byFood[foods["Cebolla cocida"]].ActionType.Should().Be(ActionType.Avoid);
        byFood[foods["Arroz blanco cocido"]].ActionType.Should().Be(ActionType.Suggest);
        byFood[foods["Pan de trigo blanco"]].ActionType.Should().Be(ActionType.Substitute);
        byFood[foods["Pan de trigo blanco"]].SubstituteFoodId.Should().Be(foods["Arroz blanco cocido"]);
        recommendation.Items.Should().OnlyContain(item => item.Reasoning!.StartsWith("Riesgo estimado por el modelo"));
    }

    [SkippableFact]
    public void Startup_WithAnotherActiveVersion_FailsNamingBothVersions()
    {
        Skip.IfNot(Available, "Docker no disponible; se omite.");
        using var factory = XgboostFactory();

        var act = () => factory.Services;

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{RuleVersionName}*{ModelVersionName}*");
    }

    [SkippableFact]
    public void Startup_WithWrongHash_Fails()
    {
        Skip.IfNot(Available, "Docker no disponible; se omite.");
        using var factory = XgboostFactory(sha256: new string('0', 64));

        var act = () => factory.Services;

        act.Should().Throw<InvalidOperationException>().WithMessage("*XgboostModelSha256*");
    }

    [SkippableFact]
    public void Startup_WithMissingFile_Fails()
    {
        Skip.IfNot(Available, "Docker no disponible; se omite.");
        using var factory = XgboostFactory(modelPath: Path.Combine(AppContext.BaseDirectory, "TestData", "no-existe.onnx"));

        var act = () => factory.Services;

        act.Should().Throw<InvalidOperationException>().WithMessage("*XgboostModelPath*no existe*");
    }

    [SkippableFact]
    public void Startup_XgboostWithoutOptions_FailsValidation()
    {
        Skip.IfNot(Available, "Docker no disponible; se omite.");
        using var factory = CreateFactory("Xgboost");

        var act = () => factory.Services;

        act.Should().Throw<OptionsValidationException>().WithMessage("*XgboostModelPath*");
    }

    [SkippableFact]
    public void Startup_UnknownEngineKind_FailsValidation()
    {
        Skip.IfNot(Available, "Docker no disponible; se omite.");
        using var factory = CreateFactory("Forest");

        var act = () => factory.Services;

        act.Should().Throw<OptionsValidationException>().WithMessage("*EngineKind*Forest*");
    }

    private static async Task<(Guid Id, string KeycloakId)> SeedPatientWithHistoryAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Patient).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId, $"user-{Guid.NewGuid():N}@cauce.local", "Paciente", roleId, TestPatientCodes.Next());
        db.Users.Add(user);

        var dob = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-35);
        db.PatientProfiles.Add(PatientProfile.Create(
            Guid.NewGuid(), user.Id, dob, BiologicalSex.Female, 70m, 175m, IbsSubtype.IbsD, null, null, DateTime.UtcNow));

        var names = new[]
        {
            "Cebolla cocida", "Pan de trigo blanco", "Arroz blanco cocido", "Pollo a la plancha", "Naranja", "Yogur natural sin lactosa"
        };
        var foodIds = await db.FoodItems.AsNoTracking().Where(f => names.Contains(f.Name)).Select(f => f.Id).ToListAsync();
        var timestamp = DateTime.UtcNow.AddDays(-1);
        foreach (var foodId in foodIds)
        {
            db.Meals.Add(Meal.Register(
                Guid.NewGuid(), Guid.NewGuid(), user.Id, MealTime.Lunch, timestamp, timestamp,
                new[] { new MealItemInput(foodId, null, 150m, MeasurementUnit.Grams) }, DateTime.UtcNow));
        }

        await db.SaveChangesAsync();
        return (user.Id, keycloakId);
    }

    private static async Task<string> SeedAssignedNutritionistAsync(WebApplicationFactory<Program> factory, Guid patientId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        var roleId = await db.UserRoles.Where(r => r.RoleName == UserRoles.Nutritionist).Select(r => r.RoleId).FirstAsync();
        var keycloakId = Guid.NewGuid().ToString();
        var user = User.CreateNutritionist(Guid.NewGuid(), keycloakId, $"nutri-{Guid.NewGuid():N}@cauce.local", "Nutri", roleId);
        user.Activate();
        db.Users.Add(user);
        db.NutritionistPatients.Add(NutritionistPatient.Establish(Guid.NewGuid(), user.Id, patientId, null, DateTime.UtcNow));

        await db.SaveChangesAsync();
        return keycloakId;
    }

    private static HttpClient AuthedClient(WebApplicationFactory<Program> factory, string keycloakId, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtBuilder.Build(keycloakId, "user@cauce.local", role));
        return client;
    }

    private static async Task<HttpResponseMessage> PostWithKey(HttpClient client, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private sealed class FixedDescriptorEngine : IRecommendationEngine
    {
        public FixedDescriptorEngine(ModelVersionDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public ModelVersionDescriptor Descriptor { get; }

        public Task<RecommendationEngineResult> ScoreFoodsAsync(
            RecommendationEngineInput input,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
