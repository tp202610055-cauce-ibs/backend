using Cauce.Api.Contracts.Recommendations;
using Cauce.Application.Common.Idempotency;
using Cauce.Application.Common.Models;
using Cauce.Application.Recommendations.Dtos;
using Cauce.Application.Recommendations.UseCases.DeliverRecommendation;
using Cauce.Application.Recommendations.UseCases.GenerateRecommendation;
using Cauce.Application.Recommendations.UseCases.ListPatientRecommendations;
using Cauce.Application.Recommendations.UseCases.SubmitFeedback;
using Cauce.Domain.Recommendations.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers.Recommendations;

/// <summary>
/// Endpoints del paciente para solicitar, listar, entregar y retroalimentar sus recomendaciones.
/// </summary>
[Route("api/v{version:apiVersion}/recommendations")]
[Authorize(Policy = "Patient")]
[EnableRateLimiting(Configuration.RateLimitingPolicies.DefaultAuthenticated)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class PatientRecommendationsController : BaseApiController
{
    private readonly ISender _mediator;
    private readonly IIdempotencyContext _idempotencyContext;

    /// <summary>
    /// Inicializa el controlador con sus dependencias.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    /// <param name="idempotencyContext">Contexto de idempotencia de la petición.</param>
    public PatientRecommendationsController(ISender mediator, IIdempotencyContext idempotencyContext)
    {
        _mediator = mediator;
        _idempotencyContext = idempotencyContext;
    }

    /// <summary>
    /// Solicita la generación de una nueva recomendación para el paciente autenticado. Requiere el
    /// header <c>Idempotency-Key</c>.
    /// </summary>
    /// <param name="idempotencyKey">Clave de idempotencia (UUID v4) del header <c>Idempotency-Key</c>.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El resultado de la generación.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(GenerateRecommendationResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GenerateRecommendationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Generate(
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateRecommendationCommand(idempotencyKey), ct);
        var statusCode = _idempotencyContext.WasReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created;
        return StatusCode(statusCode, result);
    }

    /// <summary>
    /// Lista, paginadas, las recomendaciones del paciente autenticado.
    /// </summary>
    /// <param name="status">Estado por el que filtrar, o <see langword="null"/> para todos.</param>
    /// <param name="page">Número de página.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La página de recomendaciones del paciente.</returns>
    [HttpGet("me")]
    [ProducesResponseType(typeof(PagedResult<RecommendationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListMine(
        [FromQuery] RecommendationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListPatientRecommendationsQuery(status, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// Marca una recomendación como entregada cuando el paciente la visualiza. Requiere el header
    /// <c>Idempotency-Key</c>.
    /// </summary>
    /// <param name="id">Identificador de la recomendación.</param>
    /// <param name="idempotencyKey">Clave de idempotencia del header <c>Idempotency-Key</c>.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Sin contenido.</returns>
    [HttpPost("{id:guid}/deliver")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deliver(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken ct)
    {
        await _mediator.Send(new DeliverRecommendationCommand(id, idempotencyKey), ct);
        return NoContent();
    }

    /// <summary>
    /// Envía la retroalimentación del paciente sobre una recomendación entregada. Requiere el header
    /// <c>Idempotency-Key</c>.
    /// </summary>
    /// <param name="id">Identificador de la recomendación.</param>
    /// <param name="request">Datos de la retroalimentación.</param>
    /// <param name="idempotencyKey">Clave de idempotencia del header <c>Idempotency-Key</c>.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Sin contenido.</returns>
    [HttpPost("{id:guid}/feedback")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitFeedback(
        Guid id,
        [FromBody] SubmitFeedbackRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken ct)
    {
        var command = new SubmitFeedbackCommand(id, request.WasApplied, request.Outcome, request.Comment, idempotencyKey);
        await _mediator.Send(command, ct);
        return NoContent();
    }
}
