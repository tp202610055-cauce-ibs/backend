using Cauce.Api.Configuration;
using Cauce.Application.Identity.UseCases.GetCurrentConsent;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoint público del documento de consentimiento informado vigente. Es anónimo porque el paciente
/// lo consulta antes de registrarse, cuando todavía no tiene cuenta ni token (US01).
/// </summary>
[Route("api/v{version:apiVersion}/consent")]
[AllowAnonymous]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ConsentController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public ConsentController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Devuelve la versión, el texto y el hash SHA-256 del documento de consentimiento vigente. El
    /// cliente debe enviar en <c>POST /api/v1/auth/register</c> exactamente la versión y el hash que
    /// recibe aquí; de lo contrario el registro se rechaza con 400 <c>consent_text_mismatch</c>.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El documento de consentimiento vigente.</returns>
    [HttpGet("current")]
    [EnableRateLimiting(RateLimitingPolicies.ConsentCurrent)]
    [ProducesResponseType(typeof(CurrentConsentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCurrentConsentQuery(), ct);
        return Ok(result);
    }
}
