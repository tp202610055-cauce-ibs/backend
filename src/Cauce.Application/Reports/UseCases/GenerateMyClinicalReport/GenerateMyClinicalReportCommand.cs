using MediatR;

namespace Cauce.Application.Reports.UseCases.GenerateMyClinicalReport;

/// <summary>
/// Comando para que el paciente autenticado genere su propio reporte clínico en PDF cifrado (US24),
/// cubriendo sus últimos 90 días. El nutricionista asignado (si existe) se incluye en el reporte; de
/// lo contrario esa sección se omite. El paciente se resuelve del JWT.
/// </summary>
public sealed record GenerateMyClinicalReportCommand : IRequest<GenerateMyClinicalReportResult>;

/// <summary>
/// Resultado de la generación del autoreporte clínico del paciente.
/// </summary>
/// <param name="ReportId">Identificador del reporte.</param>
/// <param name="PresignedUrl">URL prefirmada de descarga.</param>
/// <param name="PresignedUrlExpiresAt">Momento de expiración de la URL, en UTC.</param>
public sealed record GenerateMyClinicalReportResult(
    Guid ReportId,
    string PresignedUrl,
    DateTime PresignedUrlExpiresAt);
