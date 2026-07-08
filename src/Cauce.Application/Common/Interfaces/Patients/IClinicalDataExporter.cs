namespace Cauce.Application.Common.Interfaces.Patients;

/// <summary>
/// Compila los datos personales y clínicos de un paciente en un archivo ZIP de CSVs, lo publica en el
/// almacenamiento de objetos y devuelve una URL de descarga prefirmada. Materializa el derecho a la
/// portabilidad de datos de la Ley N° 29733 (US25).
/// </summary>
public interface IClinicalDataExporter
{
    /// <summary>
    /// Exporta todos los datos del paciente indicado.
    /// </summary>
    /// <param name="patientUserId">Identificador de la cuenta del paciente (clave primaria en <c>users</c>).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La URL de descarga prefirmada, su vencimiento y el conteo de filas por entidad.</returns>
    Task<ClinicalDataExport> ExportAsync(Guid patientUserId, CancellationToken ct = default);
}

/// <summary>
/// Resultado de una exportación de datos del paciente.
/// </summary>
/// <param name="DownloadUrl">URL de descarga prefirmada del archivo ZIP.</param>
/// <param name="ExpiresAtUtc">Momento de vencimiento de la URL, en UTC.</param>
/// <param name="Counts">Conteo de filas exportadas por entidad, para trazabilidad de auditoría.</param>
public sealed record ClinicalDataExport(
    string DownloadUrl,
    DateTime ExpiresAtUtc,
    IReadOnlyDictionary<string, int> Counts);
