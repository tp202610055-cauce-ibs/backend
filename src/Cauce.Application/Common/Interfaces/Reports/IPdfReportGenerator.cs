namespace Cauce.Application.Common.Interfaces.Reports;

/// <summary>
/// Genera un reporte clínico en PDF cifrado, lo almacena y devuelve una URL prefirmada y la
/// contraseña (DEC-B5-08).
/// </summary>
public interface IPdfReportGenerator
{
    /// <summary>
    /// Genera el reporte del paciente para el período indicado.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="periodStart">Inicio del período.</param>
    /// <param name="periodEnd">Fin del período.</param>
    /// <param name="nutritionistId">Identificador del nutricionista.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El resultado con la ruta, el tamaño, la contraseña y la URL prefirmada.</returns>
    Task<PdfReportResult> GenerateAsync(
        Guid patientId,
        DateOnly periodStart,
        DateOnly periodEnd,
        Guid nutritionistId,
        CancellationToken ct = default);
}

/// <summary>
/// Resultado de la generación de un reporte clínico.
/// </summary>
/// <param name="ReportId">Identificador del reporte.</param>
/// <param name="ObjectStoragePath">Ruta del objeto en el almacenamiento.</param>
/// <param name="FileSizeBytes">Tamaño del archivo, en bytes.</param>
/// <param name="Password">Contraseña del PDF cifrado.</param>
/// <param name="PresignedUrl">URL prefirmada de descarga.</param>
/// <param name="PresignedUrlExpiresAt">Momento de expiración de la URL, en UTC.</param>
public sealed record PdfReportResult(
    Guid ReportId,
    string ObjectStoragePath,
    int FileSizeBytes,
    string Password,
    string PresignedUrl,
    DateTime PresignedUrlExpiresAt);
