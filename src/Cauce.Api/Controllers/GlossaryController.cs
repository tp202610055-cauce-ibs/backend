using Cauce.Api.Configuration;
using Cauce.Application.ClinicalRegistry.UseCases.GetGlossary;
using Cauce.Application.ClinicalRegistry.UseCases.SearchGlossary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints de consulta del glosario clínico-nutricional (US27). Accesibles para cualquier usuario
/// autenticado; la definición devuelta depende del rol del solicitante.
/// </summary>
[Route("api/v{version:apiVersion}/glossary")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
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
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGlossaryQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Busca términos del glosario por coincidencia de texto (insensible a mayúsculas y a tildes).
    /// </summary>
    /// <param name="q">Texto a buscar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los términos coincidentes.</returns>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        var result = await _mediator.Send(new SearchGlossaryQuery(q), ct);
        return Ok(result);
    }
}
