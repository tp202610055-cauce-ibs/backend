using Cauce.Api.Configuration;
using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.UseCases.GetUnifiedHistory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoint del historial unificado (comidas, síntomas y notas) del paciente autenticado.
/// </summary>
[Route("api/v{version:apiVersion}/history")]
[Authorize(Policy = "Patient")]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class HistoryController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public HistoryController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Devuelve el historial unificado del paciente autenticado en un rango de fechas,
    /// ordenado cronológicamente de forma descendente.
    /// </summary>
    /// <param name="from">Inicio del rango (UTC).</param>
    /// <param name="to">Fin del rango (UTC).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los eventos del historial.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HistoryEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUnifiedHistoryQuery(from, to), ct);
        return Ok(result);
    }
}
