using Cauce.Application.Recommendations.Dtos;
using Cauce.Application.Recommendations.UseCases.GetRecommendationById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers.Recommendations;

/// <summary>
/// Endpoint compartido para consultar el detalle de una recomendación. La autorización por recurso
/// se resuelve en el handler según el rol: el paciente debe ser el propietario y el nutricionista,
/// estar asignado al paciente.
/// </summary>
[Route("api/v{version:apiVersion}/recommendations")]
[Authorize]
[EnableRateLimiting(Configuration.RateLimitingPolicies.DefaultAuthenticated)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class RecommendationsController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con sus dependencias.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public RecommendationsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Devuelve el detalle de una recomendación.
    /// </summary>
    /// <param name="id">Identificador de la recomendación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El detalle de la recomendación.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RecommendationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRecommendationByIdQuery(id), ct);
        return Ok(result);
    }
}
