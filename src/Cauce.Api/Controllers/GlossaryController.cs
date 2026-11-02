using Cauce.Api.Configuration;
using Cauce.Api.Contracts.ClinicalRegistry;
using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.UseCases.GetGlossary;
using Cauce.Application.ClinicalRegistry.UseCases.RequestGlossaryTerm;
using Cauce.Application.ClinicalRegistry.UseCases.SearchGlossary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints de consulta del glosario clínico-nutricional (US27). Accesibles para cualquier usuario
/// autenticado; la definición devuelta depende del rol del solicitante.
/// </summary>
[Route("api/v{version:apiVersion}/glossary")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class GlossaryController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public GlossaryController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Devuelve todo el glosario ordenado alfabéticamente, con la definición apropiada al rol.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El glosario completo.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(GlossaryResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGlossaryQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Busca términos del glosario por coincidencia de texto (insensible a mayúsculas y a tildes).
    /// </summary>
    /// <param name="q">Texto a buscar, obligatorio y no vacío.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los términos coincidentes.</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(GlossaryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery, BindRequired] string q, CancellationToken ct)
    {
        var result = await _mediator.Send(new SearchGlossaryQuery(q), ct);
        return Ok(result);
    }

    /// <summary>
    /// Pide que se agregue un término al glosario. Si el mismo nutricionista ya lo pidió y sigue pendiente, devuelve esa solicitud con 200 sin duplicarla.
    /// </summary>
    /// <param name="request">Término pedido.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La solicitud pendiente: 201 si es nueva y 200 si ya existía.</returns>
    [HttpPost("term-requests")]
    [Authorize(Policy = "Nutritionist")]
    [ProducesResponseType(typeof(GlossaryTermRequestResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GlossaryTermRequestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestTerm([FromBody] RequestGlossaryTermRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RequestGlossaryTermCommand(request.Term), ct);
        return result.Created
            ? StatusCode(StatusCodes.Status201Created, result.Request)
            : Ok(result.Request);
    }
}
