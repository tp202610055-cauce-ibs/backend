namespace Cauce.Api.Contracts.Reports;

/// <summary>
/// Solicitud de generación de un reporte clínico para un paciente en un período.
/// </summary>
/// <param name="PeriodStart">Inicio del período.</param>
/// <param name="PeriodEnd">Fin del período.</param>
public sealed record GenerateClinicalReportRequest(DateOnly PeriodStart, DateOnly PeriodEnd);
