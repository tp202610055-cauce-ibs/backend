using System.Net;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Identity;
using FluentAssertions;
using PdfSharp.Pdf.IO;

namespace Cauce.Api.IntegrationTests.Patients;

/// <summary>
/// Pruebas de integración de la descarga del comprobante de consentimiento en PDF (US01 CA04): PDF
/// descargable cuando existe consentimiento y 404 cuando no. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ConsentPdfApiTests : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public ConsentPdfApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task GetConsentPdf_WithConsentRecord_ReturnsDownloadablePdf()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedConsentAsync(patient.Id, "1.0");

        var client = PatientClient(patient.KeycloakId, patient.Email);
        var response = await client.GetAsync("/api/v1/patients/me/consent/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("1.0");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");

        // El PDF no está cifrado (dato propio del paciente): abre sin contraseña.
        using var document = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.ReadOnly);
        document.PageCount.Should().BeGreaterThan(0);
    }

    [SkippableFact]
    public async Task GetConsentPdf_WithoutConsentRecord_Returns404()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var client = PatientClient(patient.KeycloakId, patient.Email);
        var response = await client.GetAsync("/api/v1/patients/me/consent/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task SeedConsentAsync(Guid userId, string version)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var consent = ConsentRecord.Capture(Guid.NewGuid(), userId, version, new string('a', 64), "127.0.0.1", DateTime.UtcNow);
        db.Set<ConsentRecord>().Add(consent);
        await db.SaveChangesAsync();
    }
}
