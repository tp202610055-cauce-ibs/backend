using Cauce.Api.Configuration;
using Cauce.Api.Contracts.ClinicalRegistry;
using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.UseCases.CreateClinicalNote;
using Cauce.Application.ClinicalRegistry.UseCases.GetClinicalNotes;
using Cauce.Application.Common.Idempotency;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints del paciente para registrar y consultar sus notas clínicas.
/// </summary>
[Route("api/v{version:apiVersion}/clinical-notes")]
[Authorize(Policy = "Patient")]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ClinicalNotesController : BaseApiController
{
    private readonly ISender _mediator;
    private readonly IIdempotencyContext _idempotencyContext;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    /// <param name="idempotencyContext">Contexto de idempotencia de la petición.</param>
    public ClinicalNotesController(ISender mediator, IIdempotencyContext idempotencyContext)
    {
        _mediator = mediator;
        _idempotencyContext = idempotencyContext;
    }

    /// <summary>
    /// Crea una nota clínica del paciente autenticado, asociada a una comida o a un síntoma.
    /// </summary>
    /// <param name="request">Datos de la nota.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador de la nota creada, con código 201; 200 si es un reintento idempotente.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CreateClinicalNoteResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreateClinicalNoteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateClinicalNoteRequest request, CancellationToken ct)
    {
        var clientGuid = ResolveClientGuid(request.ClientGuid);
        var command = new CreateClinicalNoteCommand(clientGuid, request.MealId, request.SymptomId, request.Content);
        var result = await _mediator.Send(command, ct);

        var statusCode = _idempotencyContext.WasReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created;
        return StatusCode(statusCode, result);
    }

    /// <summary>
    /// Lista las notas clínicas del paciente autenticado en un rango de fechas.
    /// </summary>
    /// <param name="from">Inicio del rango (UTC).</param>
    /// <param name="to">Fin del rango (UTC).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las notas clínicas.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicalNoteSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetClinicalNotesQuery(from, to), ct);
        return Ok(result);
    }
}
