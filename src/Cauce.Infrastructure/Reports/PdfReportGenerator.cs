using System.Security.Cryptography;
using Cauce.Application.Common.Interfaces.Reports;
using Cauce.Application.Common.Interfaces.Storage;
using Cauce.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Implementación de <see cref="IPdfReportGenerator"/> que consolida los datos clínicos, compone el
/// PDF con QuestPDF, lo cifra con una contraseña aleatoria mediante PdfSharp, lo sube a MinIO y
/// devuelve una URL prefirmada. La contraseña se devuelve al llamador pero nunca se persiste ni se
/// registra (DEC-B5-08, DEC-B5-11).
/// </summary>
public sealed class PdfReportGenerator : IPdfReportGenerator
{
    private const string PasswordAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    static PdfReportGenerator()
    {
        // Licencia comunitaria de QuestPDF (uso sin fines de lucro / proyecto de tesis).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private readonly IClinicalReportDataReader _dataReader;
    private readonly IObjectStorage _objectStorage;
    private readonly MinioOptions _minioOptions;
    private readonly ReportOptions _reportOptions;

    /// <summary>
    /// Inicializa el generador con sus dependencias.
    /// </summary>
    /// <param name="dataReader">Lector de datos clínicos del reporte.</param>
    /// <param name="objectStorage">Almacenamiento de objetos.</param>
    /// <param name="minioOptions">Opciones de MinIO (bucket de reportes).</param>
    /// <param name="reportOptions">Opciones de reporte.</param>
    public PdfReportGenerator(
        IClinicalReportDataReader dataReader,
        IObjectStorage objectStorage,
        IOptions<MinioOptions> minioOptions,
        IOptions<ReportOptions> reportOptions)
    {
        _dataReader = dataReader;
        _objectStorage = objectStorage;
        _minioOptions = minioOptions.Value;
        _reportOptions = reportOptions.Value;
    }

    /// <inheritdoc />
    public async Task<PdfReportResult> GenerateAsync(
        Guid patientId,
        DateOnly periodStart,
        DateOnly periodEnd,
        Guid nutritionistId,
        CancellationToken ct = default)
    {
        var reportId = Guid.NewGuid();
        var data = await _dataReader
            .GetReportDataAsync(patientId, periodStart, periodEnd, nutritionistId, ct)
            .ConfigureAwait(false);

        var pdfBytes = new ClinicalReportDocument(data).GeneratePdf();
        var password = GeneratePassword(_reportOptions.PasswordLength);
        var encryptedBytes = Encrypt(pdfBytes, password);

        var objectKey = $"{patientId:N}/{reportId:N}.pdf";
        await using (var uploadStream = new MemoryStream(encryptedBytes, writable: false))
        {
            await _objectStorage
                .UploadAsync(_minioOptions.ReportsBucket, objectKey, uploadStream, "application/pdf", ct)
                .ConfigureAwait(false);
        }

        var validity = TimeSpan.FromHours(_reportOptions.PresignedUrlValidityHours);
        var presignedUrl = await _objectStorage
            .GetPresignedUrlAsync(_minioOptions.ReportsBucket, objectKey, validity, ct)
            .ConfigureAwait(false);

        return new PdfReportResult(
            reportId,
            objectKey,
            encryptedBytes.Length,
            password,
            presignedUrl,
            DateTime.UtcNow + validity);
    }

    private static byte[] Encrypt(byte[] pdfBytes, string password)
    {
        using var input = new MemoryStream(pdfBytes, writable: false);
        var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        // Al fijar contraseñas, PdfSharp activa el cifrado con su algoritmo por defecto (AES). Se
        // restringe además la modificación y la extracción de contenido.
        var security = document.SecuritySettings;
        security.UserPassword = password;
        security.OwnerPassword = GeneratePassword(24);
        security.PermitModifyDocument = false;
        security.PermitExtractContent = false;

        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    private static string GeneratePassword(int length)
    {
        return RandomNumberGenerator.GetString(PasswordAlphabet, length);
    }
}
