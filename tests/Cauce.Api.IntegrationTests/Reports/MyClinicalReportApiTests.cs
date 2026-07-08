using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Pdf.IO;

namespace Cauce.Api.IntegrationTests.Reports;

/// <summary>
/// Pruebas end-to-end del autoreporte clínico del paciente (US24): genera el PDF cifrado de sus últimos
/// 90 días, lo entrega por URL prefirmada y envía la contraseña por correo aparte, omitiendo la sección
/// del nutricionista cuando no hay uno asignado. Requieren Docker (PostgreSQL + Redis + MinIO efímeros).
/// </summary>
[Trait("Category", "Integration")]
public sealed class MyClinicalReportApiTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public MyClinicalReportApiTests(PostgresFixture postgres, RedisFixture redis, MinioFixture minio)
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

    [SkippableFact]
    public async Task GenerateMyReport_NoData_Returns422()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsync("/api/v1/patients/me/report", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [SkippableFact]
    public async Task GenerateMyReport_HappyPath_ReturnsEncryptedPdfWithTwoEmailsAndAudit()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedMealAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsync("/api/v1/patients/me/report", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = body.GetProperty("presignedUrl").GetString()!;

        Factory.EmailSender.SentEmails.Should().Contain(e => e.Kind == "report-ready");
        var password = Factory.EmailSender.SentEmails.Single(e => e.Kind == "report-password").Payload;

        var audit = (await AuditLogsAsync("clinical_report", action: AuditActionType.ExportPdf)).Should().ContainSingle().Subject;
        audit.ActorUserId.Should().Be(patient.Id);

        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(url);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");

        var openWithoutPassword = () => PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.ReadOnly);
        openWithoutPassword.Should().Throw<Exception>();
        using var document = PdfReader.Open(new MemoryStream(bytes), password, PdfDocumentOpenMode.ReadOnly);
        document.PageCount.Should().BeGreaterThan(0);
    }
}
