using Cauce.Api.Configuration;
using Cauce.Api.Contracts.ClinicalRegistry;
using Cauce.Application.ClinicalRegistry.UseCases.CreateSymptom;
using Cauce.Application.ClinicalRegistry.UseCases.GetSymptomHistory;
using Cauce.Application.Common.Idempotency;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints del paciente para registrar y consultar sus síntomas.
/// </summary>
[Route("api/v{version:apiVersion}/symptoms")]
[Authorize(Policy = "Patient")]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
public sealed class SymptomsController : BaseApiController
{
    private readonly ISender _mediator;
    private readonly IIdempotencyContext _idempotencyContext;

    /// <summary>
    /// Inicializa el controlador con sus dependencias.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    /// <param name="idempotencyContext">Contexto de idempotencia de la petición.</param>
    public SymptomsController(ISender mediator, IIdempotencyContext idempotencyContext)
    {
        _mediator = mediator;
        _idempotencyContext = idempotencyContext;
    }

    /// <summary>
    /// Registra un síntoma del paciente autenticado. El servidor calcula la asociación con
    /// la comida más reciente dentro de la ventana de 4 horas. Es idempotente respecto del
    /// <c>client_guid</c>.
    /// </summary>
    /// <param name="request">Datos del síntoma.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El resultado del registro.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSymptomRequest request, CancellationToken ct)
    {
        var clientGuid = ResolveClientGuid(request.ClientGuid);
        var command = new CreateSymptomCommand(clientGuid, request.SymptomType, request.Intensity, request.OccurredAt, request.ClientCreatedAt);
        var result = await _mediator.Send(command, ct);

        var statusCode = _idempotencyContext.WasReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created;
        return StatusCode(statusCode, result);
    }

    /// <summary>
    /// Devuelve el historial paginado de síntomas del paciente autenticado.
    /// </summary>
    /// <param name="from">Inicio del rango (UTC).</param>
    /// <param name="to">Fin del rango (UTC).</param>
    /// <param name="page">Número de página.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El historial de síntomas.</returns>
    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSymptomHistoryQuery(from, to, page, pageSize), ct);
        return Ok(result);
    }

    private Guid ResolveClientGuid(Guid? bodyClientGuid)
    {
        Guid? headerGuid = null;
        if (Request.Headers.TryGetValue("Idempotency-Key", out var headerValues)
            && Guid.TryParse(headerValues.ToString(), out var parsed))
        {
            headerGuid = parsed;
        }

        if (bodyClientGuid.HasValue && headerGuid.HasValue && bodyClientGuid.Value != headerGuid.Value)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("clientGuid", "El Idempotency-Key no coincide con el client_guid del cuerpo.")
            });
        }

        return bodyClientGuid ?? headerGuid ?? Guid.Empty;
    }
}
