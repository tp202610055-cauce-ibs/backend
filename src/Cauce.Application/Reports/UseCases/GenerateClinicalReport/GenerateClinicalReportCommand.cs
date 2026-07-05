using Cauce.Application.Common.Idempotency;
using MediatR;

namespace Cauce.Application.Reports.UseCases.GenerateClinicalReport;

/// <summary>
/// Comando para que un nutricionista genere el reporte clínico de un paciente en un período. Es
/// idempotente respecto del <see cref="ClientGuid"/> tomado del header <c>Idempotency-Key</c>. El
/// nutricionista se resuelve del JWT.
/// </summary>
/// <param name="PatientId">Identificador del paciente.</param>
/// <param name="PeriodStart">Inicio del período.</param>
/// <param name="PeriodEnd">Fin del período.</param>
/// <param name="ClientGuid">Clave de idempotencia (UUID v4).</param>
public sealed record GenerateClinicalReportCommand(
    Guid PatientId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    Guid ClientGuid) : IRequest<GenerateClinicalReportResult>, IIdempotentCommand;

/// <summary>
/// Resultado de la generación de un reporte clínico.
/// </summary>
/// <param name="ReportId">Identificador del reporte.</param>
/// <param name="PresignedUrl">URL prefirmada de descarga.</param>
/// <param name="PresignedUrlExpiresAt">Momento de expiración de la URL, en UTC.</param>
public sealed record GenerateClinicalReportResult(
    Guid ReportId,
    string PresignedUrl,
    DateTime PresignedUrlExpiresAt);
