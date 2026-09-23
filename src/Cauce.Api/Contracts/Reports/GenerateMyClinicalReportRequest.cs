namespace Cauce.Api.Contracts.Reports;

/// <summary>
/// Cuerpo opcional de la solicitud del autoreporte clínico del paciente (US24). Si se omite el
/// cuerpo, o si ambos extremos llegan en <see langword="null"/>, el reporte cubre la ventana por
/// defecto de 90 días hacia atrás.
/// </summary>
/// <param name="PeriodStart">Inicio del período, o <see langword="null"/>.</param>
/// <param name="PeriodEnd">Fin del período, o <see langword="null"/>.</param>
public sealed record GenerateMyClinicalReportRequest(DateOnly? PeriodStart = null, DateOnly? PeriodEnd = null);
