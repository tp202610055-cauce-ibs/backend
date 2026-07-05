using Cauce.Domain.Common;
using Cauce.Domain.Reports.Exceptions;

namespace Cauce.Domain.Reports;

/// <summary>
/// Metadatos de un reporte clínico emitido: registra el hecho de la emisión para trazabilidad,
/// sin almacenar el contenido del PDF, la contraseña ni el URL prefirmado (que es efímero).
/// </summary>
public sealed class ClinicalReportMetadata : Entity
{
    /// <summary>
    /// Identificador del paciente reportado.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Identificador del nutricionista que generó el reporte.
    /// </summary>
    public Guid GeneratedByNutritionistId { get; private set; }

    /// <summary>
    /// Inicio del período reportado.
    /// </summary>
    public DateOnly PeriodStart { get; private set; }

    /// <summary>
    /// Fin del período reportado.
    /// </summary>
    public DateOnly PeriodEnd { get; private set; }

    /// <summary>
    /// Ruta del objeto en el almacenamiento (MinIO).
    /// </summary>
    public string ObjectStoragePath { get; private set; } = string.Empty;

    /// <summary>
    /// Tamaño del archivo generado, en bytes.
    /// </summary>
    public int FileSizeBytes { get; private set; }

    /// <summary>
    /// Momento de generación, en UTC.
    /// </summary>
    public DateTime GeneratedAt { get; private set; }

    private ClinicalReportMetadata()
    {
    }

    private ClinicalReportMetadata(
        Guid id,
        Guid patientId,
        Guid generatedByNutritionistId,
        DateOnly periodStart,
        DateOnly periodEnd,
        string objectStoragePath,
        int fileSizeBytes,
        DateTime generatedAt)
        : base(id)
    {
        PatientId = patientId;
        GeneratedByNutritionistId = generatedByNutritionistId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        ObjectStoragePath = objectStoragePath;
        FileSizeBytes = fileSizeBytes;
        GeneratedAt = generatedAt;
    }

    /// <summary>
    /// Registra los metadatos de un reporte emitido.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="generatedByNutritionistId">Identificador del nutricionista.</param>
    /// <param name="periodStart">Inicio del período.</param>
    /// <param name="periodEnd">Fin del período.</param>
    /// <param name="objectStoragePath">Ruta del objeto en el almacenamiento.</param>
    /// <param name="fileSizeBytes">Tamaño del archivo, en bytes.</param>
    /// <param name="generatedAtUtc">Momento de generación, en UTC.</param>
    /// <returns>Los metadatos del reporte.</returns>
    /// <exception cref="ReportPeriodInvalidException">Si el fin del período es anterior al inicio.</exception>
    public static ClinicalReportMetadata Register(
        Guid patientId,
        Guid generatedByNutritionistId,
        DateOnly periodStart,
        DateOnly periodEnd,
        string objectStoragePath,
        int fileSizeBytes,
        DateTime generatedAtUtc)
    {
        if (periodEnd < periodStart)
        {
            throw new ReportPeriodInvalidException("el fin del período no puede ser anterior al inicio.");
        }

        return new ClinicalReportMetadata(
            Guid.NewGuid(), patientId, generatedByNutritionistId, periodStart, periodEnd,
            objectStoragePath, fileSizeBytes, generatedAtUtc);
    }
}
