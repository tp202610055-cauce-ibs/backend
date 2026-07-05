using Cauce.Api.Contracts.Reports;
using Cauce.Application.Reports.UseCases.GenerateClinicalReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers.Reports;

/// <summary>
/// Endpoints de reportes clínicos para el nutricionista. La generación produce un PDF cifrado cuya
/// URL y contraseña se envían por correo en mensajes separados (DEC-B5-08, DEC-B5-11).
/// </summary>
[Route("api/v{version:apiVersion}/reports")]
[Authorize(Policy = "Nutritionist")]
[EnableRateLimiting(Configuration.RateLimitingPolicies.DefaultAuthenticated)]
public sealed class ReportsController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public ReportsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Genera el reporte clínico de un paciente en el período indicado. Requiere el header
    /// <c>Idempotency-Key</c> (UUID v4).
    /// </summary>
    /// <param name="id">Identificador del paciente.</param>
    /// <param name="request">Período del reporte.</param>
    /// <param name="idempotencyKey">Clave de idempotencia del header <c>Idempotency-Key</c>.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>202 con el identificador del reporte y la URL prefirmada de descarga.</returns>
    [HttpPost("patients/{id:guid}")]
    public async Task<IActionResult> Generate(
        Guid id,
        [FromBody] GenerateClinicalReportRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken ct)
    {
        var command = new GenerateClinicalReportCommand(id, request.PeriodStart, request.PeriodEnd, idempotencyKey);
        var result = await _mediator.Send(command, ct);
        return Accepted(result);
    }
}
