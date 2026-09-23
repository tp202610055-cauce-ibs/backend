using MediatR;

namespace Cauce.Application.Reports.UseCases.GenerateMyClinicalReport;

/// <summary>
/// Comando para que el paciente autenticado genere su propio reporte clínico en PDF cifrado (US24).
/// El nutricionista asignado (si existe) se incluye en el reporte; de lo contrario esa sección se
/// omite. El paciente se resuelve del JWT.
///
/// <para>El período es elegible (HU0024). Si se omiten ambos extremos se usa la ventana por defecto
/// de <c>Reports:DefaultPeriodDays</c> hacia atrás desde hoy, que es el comportamiento anterior a
/// este cambio; un cliente que no envíe cuerpo sigue funcionando igual.</para>
/// </summary>
/// <param name="PeriodStart">Inicio del período, o <see langword="null"/> para la ventana por defecto.</param>
/// <param name="PeriodEnd">Fin del período, o <see langword="null"/> para la ventana por defecto.</param>
public sealed record GenerateMyClinicalReportCommand(
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null) : IRequest<GenerateMyClinicalReportResult>;

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
