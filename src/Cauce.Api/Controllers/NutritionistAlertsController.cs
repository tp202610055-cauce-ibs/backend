using Cauce.Api.Configuration;
using Cauce.Application.Notifications.Dtos;
using Cauce.Application.Notifications.UseCases.ListNutritionistAlerts;
using Cauce.Application.Notifications.UseCases.MarkAllNutritionistAlertsRead;
using Cauce.Application.Notifications.UseCases.MarkNutritionistAlertRead;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Bandeja de avisos del nutricionista autenticado: pacientes vinculados y cambios de subtipo de SII.
/// </summary>
[Route("api/v{version:apiVersion}/nutritionists/me/alerts")]
[Authorize(Policy = "Nutritionist")]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class NutritionistAlertsController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public NutritionistAlertsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista los avisos del nutricionista, del más reciente al más antiguo, con el conteo de no leídos.
    /// </summary>
    /// <param name="page">Número de página, desde 1.</param>
    /// <param name="pageSize">Tamaño de página, de 1 a 100.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La página de avisos.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(NutritionistAlertListResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListNutritionistAlertsQuery(page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// Marca como leído un aviso propio. Repetirlo no cambia la fecha de la primera lectura.
    /// </summary>
    /// <param name="alertId">Identificador del aviso.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Sin contenido.</returns>
    [HttpPost("{alertId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid alertId, CancellationToken ct)
    {
        await _mediator.Send(new MarkNutritionistAlertReadCommand(alertId), ct);
        return NoContent();
    }

    /// <summary>
    /// Marca como leídos todos los avisos propios.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Sin contenido.</returns>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _mediator.Send(new MarkAllNutritionistAlertsReadCommand(), ct);
        return NoContent();
    }
}
