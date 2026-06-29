using Cauce.Api.Configuration;
using Cauce.Api.Contracts.ClinicalRegistry;
using Cauce.Application.ClinicalRegistry.UseCases.CreateIbsSssAssessment;
using Cauce.Application.ClinicalRegistry.UseCases.GetIbsSssEvolution;
using Cauce.Application.ClinicalRegistry.UseCases.GetLatestIbsSssAssessment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints del paciente para registrar y consultar sus evaluaciones IBS-SSS.
/// </summary>
[Route("api/v{version:apiVersion}/ibs-sss")]
[Authorize(Policy = "Patient")]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
public sealed class IbsSssController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public IbsSssController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registra una evaluación IBS-SSS del paciente autenticado. El servidor calcula el
    /// puntaje total y la categoría; una evaluación de línea base cierra el onboarding.
    /// </summary>
    /// <param name="request">Las cinco dimensiones y el tipo de evaluación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El resultado con los valores calculados, con código 201.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIbsSssAssessmentRequest request, CancellationToken ct)
    {
        var command = new CreateIbsSssAssessmentCommand(
            request.AssessmentType,
            request.PainSeverity,
            request.PainFrequency,
            request.BloatingSeverity,
            request.BowelHabitsDissatisfaction,
            request.LifeInterference);

        var result = await _mediator.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Devuelve la evolución IBS-SSS del paciente autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las evaluaciones con su diferencia respecto de la línea base.</returns>
    [HttpGet("evolution")]
    public async Task<IActionResult> GetEvolution(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetIbsSssEvolutionQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Devuelve la evaluación IBS-SSS más reciente del paciente autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La evaluación más reciente, o <see langword="null"/> si no existe.</returns>
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetLatestIbsSssAssessmentQuery(), ct);
        return Ok(result);
    }
}
