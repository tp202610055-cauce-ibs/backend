using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Patients;

/// <summary>
/// Pruebas de integración del host con el que MinIO firma las URLs de descarga. Corren contra un
/// MinIO real de Testcontainers y no contra un doble: la firma S3 cubre el host, así que lo único que
/// demuestra que el endpoint público se aplicó de verdad es mirar la URL que devuelve el propio SDK.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MinioPresignHostApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IClassFixture<MinioFixture>
{
    private const string PublicEndpoint = "archivos.cauce.local:9000";

    private readonly MinioFixture _minio;

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public MinioPresignHostApiTests(PostgresFixture postgres, RedisFixture redis, MinioFixture minio)
        : base(postgres, redis)
    {
        _minio = minio;
    }

    /// <inheritdoc />
    protected override bool ExtraAvailable => _minio.IsAvailable;

    /// <inheritdoc />
    protected override CustomWebApplicationFactory CreateFactory() => new(
        Postgres.ConnectionString,
        Redis.ConnectionString,
        minioEndpoint: _minio.Endpoint,
        minioAccessKey: _minio.AccessKey,
        minioSecretKey: _minio.SecretKey,
        minioPublicEndpoint: PublicEndpoint);

    private async Task SeedMealAsync(Guid patientId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var foodId = await db.FoodItems.AsNoTracking().Select(f => f.Id).FirstAsync();
        var consumedAt = DateTime.UtcNow.AddDays(-1);
        db.Meals.Add(Meal.Register(
            Guid.NewGuid(), Guid.NewGuid(), patientId, MealTime.Lunch, consumedAt, consumedAt,
            new[] { new MealItemInput(foodId, null, 100m, MeasurementUnit.Grams) }, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task ExportMyData_SignsAgainstThePublicEndpoint()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .GetAsync("/api/v1/patients/me/export-data");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = new Uri(body.GetProperty("downloadUrl").GetString()!);

        // El host con el que el backend habla con MinIO no es el host desde el que descarga el
        // paciente: dentro de Docker el primero no existe fuera del contenedor.
        url.Authority.Should().Be(PublicEndpoint);
        url.ToString().Should().NotContain(_minio.Endpoint);
    }

    [SkippableFact]
    public async Task ExportMyData_KeepsTheSignatureOnThePublicHost()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .GetAsync("/api/v1/patients/me/export-data");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = new Uri(body.GetProperty("downloadUrl").GetString()!);

        // La firma viaja en la query y se calculó sobre el host público; reescribirla después de
        // firmar la invalidaría, que es justamente por lo que hay un cliente de firma aparte.
        url.Query.Should().Contain("X-Amz-Signature");
        url.Query.Should().Contain("X-Amz-Credential");
    }

    [SkippableFact]
    public async Task ExportMyData_UrlExpiresWithinTheConfiguredWindow()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .GetAsync("/api/v1/patients/me/export-data");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var expiresAt = body.GetProperty("expiresAtUtc").GetDateTime();

        // Una hora por defecto: la URL es una credencial al portador sobre el expediente completo y
        // el cliente descarga en el acto. Antes vivía siete días.
        expiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromMinutes(5));

        var url = new Uri(body.GetProperty("downloadUrl").GetString()!);
        url.Query.Should().Contain("X-Amz-Expires=3600");
    }

    [SkippableFact]
    public async Task GenerateMyReport_AlsoSignsAgainstThePublicEndpoint()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedMealAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsync("/api/v1/patients/me/report", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = new Uri(body.GetProperty("presignedUrl").GetString()!);

        url.Authority.Should().Be(PublicEndpoint);
    }
}
