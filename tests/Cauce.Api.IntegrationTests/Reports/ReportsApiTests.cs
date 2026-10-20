using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Pdf.IO;

namespace Cauce.Api.IntegrationTests.Reports;

/// <summary>
/// Pruebas end-to-end del flujo de reportes clínicos (DEC-B5-08/11): autorización, ausencia de datos,
/// generación del PDF cifrado, subida a MinIO, URL prefirmada, dos correos separados y auditoría
/// <c>export_pdf</c>. Requieren Docker (PostgreSQL + Redis + MinIO efímeros).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReportsApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public ReportsApiTests(PostgresFixture postgres, RedisFixture redis, MinioFixture minio)
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
    public async Task Generate_NutritionistNotAssigned_Returns403()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();

        var response = await GenerateReport(nutritionist, patient.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task Generate_NoDataInPeriod_Returns422()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var response = await GenerateReport(nutritionist, patient.Id);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [SkippableFact]
    public async Task Generate_AsPatient_Returns403()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var request = new { periodStart = Today().AddDays(-7), periodEnd = Today() };
        var response = await PostWithKey(
            PatientClient(patient.KeycloakId, patient.Email),
            $"/api/v1/reports/patients/{patient.Id}",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task Generate_HappyPath_ReturnsAcceptedWithTwoEmailsAndExportAudit()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedMealAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var response = await GenerateReport(nutritionist, patient.Id);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("reportId").GetGuid().Should().NotBeEmpty();
        body.GetProperty("presignedUrl").GetString().Should().NotBeNullOrEmpty();

        Factory.EmailSender.SentEmails.Should().Contain(e => e.Kind == "report-ready");
        Factory.EmailSender.SentEmails.Should().Contain(e => e.Kind == "report-password");

        var audit = (await AuditLogsAsync("clinical_report", action: AuditActionType.ExportPdf)).Should().ContainSingle().Subject;
        audit.ActorUserId.Should().Be(nutritionist.Id);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.ClinicalReportsMetadata.AsNoTracking().CountAsync(m => m.PatientId == patient.Id)).Should().Be(1);
    }

    [SkippableFact]
    public async Task Generate_HappyPath_ProducesDownloadableEncryptedPdf()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedMealAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var response = await GenerateReport(nutritionist, patient.Id);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = body.GetProperty("presignedUrl").GetString()!;
        var password = Factory.EmailSender.SentEmails.Single(e => e.Kind == "report-password").Payload;

        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(url);

        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");

        // Sin contraseña no se puede abrir (cifrado); con la contraseña del correo, sí.
        var openWithoutPassword = () => PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.ReadOnly);
        openWithoutPassword.Should().Throw<Exception>();

        using var document = PdfReader.Open(new MemoryStream(bytes), password, PdfDocumentOpenMode.ReadOnly);
        document.PageCount.Should().BeGreaterThan(0);
    }

    [SkippableFact]
    public async Task Generate_PeriodsFromTestCaseCP059_RejectAnEmptyJulyAndAcceptJulyToToday()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedMealAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);
        var julyFirst = new DateOnly(2026, 7, 1);

        var emptyJuly = await GenerateReport(nutritionist, patient.Id, julyFirst, new DateOnly(2026, 7, 31));
        var julyToToday = await GenerateReport(nutritionist, patient.Id, julyFirst, Today());

        emptyJuly.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await emptyJuly.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()
            .Should().Be("patient_has_no_data_in_period");
        julyToToday.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [SkippableFact]
    public async Task Generate_SingleDayPeriod_IsAccepted()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedMealAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);
        var mealDay = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        var response = await GenerateReport(nutritionist, patient.Id, mealDay, mealDay);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    private Task<HttpResponseMessage> GenerateReport((Guid Id, string KeycloakId, string Email) nutritionist, Guid patientId) =>
        GenerateReport(nutritionist, patientId, Today().AddDays(-7), Today());

    private Task<HttpResponseMessage> GenerateReport(
        (Guid Id, string KeycloakId, string Email) nutritionist, Guid patientId, DateOnly periodStart, DateOnly periodEnd)
    {
        var request = new { periodStart, periodEnd };
        return PostWithKey(
            NutritionistClient(nutritionist.KeycloakId, nutritionist.Email),
            $"/api/v1/reports/patients/{patientId}",
            request);
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

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
}
