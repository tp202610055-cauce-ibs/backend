using Cauce.Api.Contracts.Recommendations;
using Cauce.Application.Recommendations.UseCases.ApproveRecommendation;
using Cauce.Application.Recommendations.UseCases.ListPendingReviewForNutritionist;
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
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectRecommendationRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken ct)
    {
        await _mediator.Send(new RejectRecommendationCommand(id, request.Reason, idempotencyKey), ct);
        return NoContent();
    }
}
