namespace Cauce.Api.Contracts.Reports;

/// <summary>
/// Solicitud de generación de un reporte clínico para un paciente en un período.
/// </summary>
/// <param name="PeriodStart">Inicio del período; puede ser igual al fin y la duración no tiene tope.</param>
/// <param name="PeriodEnd">Fin del período, incluido; no puede ser futuro.</param>
public sealed record GenerateClinicalReportRequest(DateOnly PeriodStart, DateOnly PeriodEnd);
