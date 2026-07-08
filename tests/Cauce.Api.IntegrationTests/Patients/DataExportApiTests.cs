using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Patients;

/// <summary>
/// Pruebas de integración de la exportación de portabilidad de datos del paciente (US25): happy path con
/// descarga del ZIP desde MinIO, presencia de encabezados aunque no haya filas (CA02) y auditoría
/// <c>export</c>. Requieren Docker (PostgreSQL + Redis + MinIO efímeros).
/// </summary>
[Trait("Category", "Integration")]
public sealed class DataExportApiTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IClassFixture<MinioFixture>
{
    private static readonly string[] ExpectedCsvNames =
    [
        "profile.csv", "allergies.csv", "meals.csv", "symptoms.csv", "ibs_sss_assessments.csv",
        "recommendations.csv", "recommendation_feedback.csv", "consent_records.csv", "audit_logs.csv"
    ];

    private readonly MinioFixture _minio;

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public DataExportApiTests(PostgresFixture postgres, RedisFixture redis, MinioFixture minio)
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
        minioSecretKey: _minio.SecretKey);

    [SkippableFact]
    public async Task ExportMyData_HappyPath_ReturnsDownloadableZipAndAudits()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedMealAsync(patient.Id);
        await SeedConsentAsync(patient.Id);

        var client = PatientClient(patient.KeycloakId, patient.Email);
        var response = await client.GetAsync("/api/v1/patients/me/export-data");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = body.GetProperty("downloadUrl").GetString();
        url.Should().NotBeNullOrEmpty();

        var entries = await DownloadZipEntriesAsync(url!);
        entries.Keys.Should().BeEquivalentTo(ExpectedCsvNames);
        entries["meals.csv"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCountGreaterThan(1);
        entries["consent_records.csv"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCountGreaterThan(1);

        var audit = (await AuditLogsAsync("PatientData", patient.Id, AuditActionType.Export)).Should().ContainSingle().Subject;
        audit.ActorUserId.Should().Be(patient.Id);
        (audit.AdditionalContext ?? string.Empty).Should().Contain("counts");
    }

    [SkippableFact]
    public async Task ExportMyData_OnlyProfile_AllCsvHeadersPresentEvenWhenEmpty()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var client = PatientClient(patient.KeycloakId, patient.Email);
        var response = await client.GetAsync("/api/v1/patients/me/export-data");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = body.GetProperty("downloadUrl").GetString()!;

        var entries = await DownloadZipEntriesAsync(url);
        entries.Keys.Should().BeEquivalentTo(ExpectedCsvNames);

        // CA02: cada CSV existe y trae encabezado; los sin filas tienen solo la línea de encabezado.
        entries["meals.csv"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle();
        entries["symptoms.csv"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle();
        entries["profile.csv"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(2);
    }

    private async Task<Dictionary<string, string>> DownloadZipEntriesAsync(string url)
    {
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(url);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            result[entry.Name] = await reader.ReadToEndAsync();
        }

        return result;
    }

    private async Task SeedMealAsync(Guid patientId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var foodId = await db.FoodItems.AsNoTracking().Select(f => f.Id).FirstAsync();
        var consumedAt = DateTime.UtcNow.AddDays(-1);
        var meal = Meal.Register(
            Guid.NewGuid(), Guid.NewGuid(), patientId, MealTime.Lunch, consumedAt, consumedAt,
            new[] { new MealItemInput(foodId, null, 100m, MeasurementUnit.Grams) }, DateTime.UtcNow);
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
    }

    private async Task SeedConsentAsync(Guid userId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var consent = ConsentRecord.Capture(Guid.NewGuid(), userId, "1.0", new string('a', 64), "127.0.0.1", DateTime.UtcNow);
        db.Set<ConsentRecord>().Add(consent);
        await db.SaveChangesAsync();
    }
}
