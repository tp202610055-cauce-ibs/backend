using Cauce.Api.Configuration;
using Cauce.Api.Contracts.ClinicalRegistry;
using Cauce.Application.ClinicalRegistry.UseCases.CreateClinicalNote;
using Cauce.Application.ClinicalRegistry.UseCases.GetClinicalNotes;
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
public sealed class ClinicalNotesController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public ClinicalNotesController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Crea una nota clínica del paciente autenticado, asociada a una comida o a un síntoma.
    /// </summary>
    /// <param name="request">Datos de la nota.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador de la nota creada, con código 201.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClinicalNoteRequest request, CancellationToken ct)
    {
        var command = new CreateClinicalNoteCommand(request.MealId, request.SymptomId, request.Content);
        var result = await _mediator.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Lista las notas clínicas del paciente autenticado en un rango de fechas.
    /// </summary>
    /// <param name="from">Inicio del rango (UTC).</param>
    /// <param name="to">Fin del rango (UTC).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las notas clínicas.</returns>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetClinicalNotesQuery(from, to), ct);
        return Ok(result);
    }
}
