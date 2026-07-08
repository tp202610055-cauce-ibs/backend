using Cauce.Api.Contracts.Recommendations;
using Cauce.Application.Common.Models;
using Cauce.Application.Recommendations.Dtos;
using Cauce.Application.Recommendations.UseCases.ApproveRecommendation;
using Cauce.Application.Recommendations.UseCases.ArchiveRecommendation;
using Cauce.Application.Recommendations.UseCases.CreateManualRecommendation;
using Cauce.Application.Recommendations.UseCases.ListPendingReviewForNutritionist;
using Cauce.Application.Recommendations.UseCases.ModifyRecommendation;
using Cauce.Application.Recommendations.UseCases.RejectRecommendation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers.Recommendations;

/// <summary>
/// Endpoints del nutricionista para revisar, aprobar y rechazar recomendaciones de sus pacientes
/// asignados.
/// </summary>
[Route("api/v{version:apiVersion}/recommendations")]
[Authorize(Policy = "Nutritionist")]
[EnableRateLimiting(Configuration.RateLimitingPolicies.DefaultAuthenticated)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class NutritionistRecommendationsController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con sus dependencias.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public NutritionistRecommendationsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista, paginadas, las recomendaciones pendientes de revisión de los pacientes asignados.
    /// </summary>
    /// <param name="page">Número de página.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La página de recomendaciones pendientes de revisión.</returns>
    [HttpGet("pending-review")]
    [ProducesResponseType(typeof(PagedResult<RecommendationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListPendingReview(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListPendingReviewForNutritionistQuery(page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// Aprueba una recomendación con la nota clínica del nutricionista. Requiere el header
    /// <c>Idempotency-Key</c>.
    /// </summary>
    /// <param name="id">Identificador de la recomendación.</param>
    /// <param name="request">Nota clínica de aprobación.</param>
    /// <param name="idempotencyKey">Clave de idempotencia del header <c>Idempotency-Key</c>.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Sin contenido.</returns>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApproveRecommendationRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken ct)
    {
        await _mediator.Send(new ApproveRecommendationCommand(id, request.Note, idempotencyKey), ct);
        return NoContent();
    }

    /// <summary>
    /// Rechaza una recomendación con el motivo del nutricionista. Requiere el header
    /// <c>Idempotency-Key</c>.
    /// </summary>
    /// <param name="id">Identificador de la recomendación.</param>
    /// <param name="request">Motivo del rechazo.</param>
    /// <param name="idempotencyKey">Clave de idempotencia del header <c>Idempotency-Key</c>.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Sin contenido.</returns>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectRecommendationRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken ct)
    {
        await _mediator.Send(new RejectRecommendationCommand(id, request.Reason, idempotencyKey), ct);
        return NoContent();
    }

    /// <summary>
    /// Crea manualmente una recomendación para un paciente asignado (US29). Queda aprobada de inmediato.
    /// </summary>
    /// <param name="request">Datos de la recomendación manual.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador de la recomendación creada, con código 201.</returns>
    [HttpPost("manual")]
    [ProducesResponseType(typeof(CreateManualRecommendationResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateManual([FromBody] CreateManualRecommendationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateManualRecommendationCommand(
                request.PatientId,
                request.Title,
                request.Description,
                request.Steps,
                request.ClinicalNote,
                request.ValidUntil),
            ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Aprueba una recomendación en revisión tras modificar sus ítems y/o su contenido (US17 CA03).
    /// </summary>
    /// <param name="id">Identificador de la recomendación.</param>
    /// <param name="request">Cambios y nota clínica de la modificación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Sin contenido.</returns>
    [HttpPost("{id:guid}/modify")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Modify(Guid id, [FromBody] ModifyRecommendationRequest request, CancellationToken ct)
    {
        await _mediator.Send(
            new ModifyRecommendationCommand(
                id, request.ClinicalNote, request.Items, request.Title, request.Description, request.Steps),
            ct);
        return NoContent();
    }

    /// <summary>
    /// Archiva una recomendación en un estado terminal aprobado (US30 CA01).
    /// </summary>
    /// <param name="id">Identificador de la recomendación.</param>
    /// <param name="request">Motivo del archivado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Sin contenido.</returns>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Archive(Guid id, [FromBody] ArchiveRecommendationRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ArchiveRecommendationCommand(id, request.Reason), ct);
        return NoContent();
    }
}
