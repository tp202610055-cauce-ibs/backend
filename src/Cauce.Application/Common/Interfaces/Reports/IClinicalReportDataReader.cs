using Cauce.Application.Reports.Contracts;

namespace Cauce.Application.Common.Interfaces.Reports;

/// <summary>
/// Lee y consolida los datos clínicos de un paciente en un período para el generador de reportes.
/// </summary>
public interface IClinicalReportDataReader
{
    /// <summary>
    /// Indica si el paciente tiene al menos una comida o un síntoma registrado en el período.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="periodStart">Inicio del período.</param>
    /// <param name="periodEnd">Fin del período.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si hay datos en el período.</returns>
    Task<bool> HasDataInPeriodAsync(Guid patientId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default);

    /// <summary>
    /// Consolida los datos del reporte del paciente para el período.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="periodStart">Inicio del período.</param>
    /// <param name="periodEnd">Fin del período.</param>
    /// <param name="nutritionistId">Identificador del nutricionista.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los datos consolidados del reporte.</returns>
    Task<ClinicalReportData> GetReportDataAsync(
        Guid patientId,
        DateOnly periodStart,
        DateOnly periodEnd,
        Guid nutritionistId,
        CancellationToken ct = default);
}
