using Cauce.Api.Configuration;
using Cauce.Api.Contracts.ClinicalRegistry;
using Cauce.Application.ClinicalRegistry.UseCases.SyncBatch;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoint de sincronización por lotes de comidas y síntomas generados en el dispositivo.
/// </summary>
[Route("api/v{version:apiVersion}/sync")]
[Authorize(Policy = "Patient")]
[EnableRateLimiting(RateLimitingPolicies.Sync)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class SyncController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public SyncController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Sincroniza un lote de comidas y síntomas. Devuelve 200 con la clasificación de cada
    /// elemento en aceptados, duplicados y errores.
    /// </summary>
    /// <param name="request">Lote de comidas y síntomas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El resultado de la sincronización.</returns>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(SyncBatchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Batch([FromBody] SyncBatchRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SyncBatchCommand(request.Meals, request.Symptoms), ct);
        return Ok(result);
    }
}
