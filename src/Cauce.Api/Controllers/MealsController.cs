using Cauce.Api.Configuration;
using Cauce.Api.Contracts.ClinicalRegistry;
using Cauce.Application.ClinicalRegistry.UseCases.CreateMeal;
using Cauce.Application.ClinicalRegistry.UseCases.GetMealHistory;
using Cauce.Application.Common.Idempotency;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints del paciente para registrar y consultar sus comidas.
/// </summary>
[Route("api/v{version:apiVersion}/meals")]
[Authorize(Policy = "Patient")]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
public sealed class MealsController : BaseApiController
{
    private readonly ISender _mediator;
    private readonly IIdempotencyContext _idempotencyContext;

    /// <summary>
    /// Inicializa el controlador con sus dependencias.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    /// <param name="idempotencyContext">Contexto de idempotencia de la petición.</param>
    public MealsController(ISender mediator, IIdempotencyContext idempotencyContext)
    {
        _mediator = mediator;
        _idempotencyContext = idempotencyContext;
    }

    /// <summary>
    /// Registra una comida del paciente autenticado. Es idempotente respecto del
    /// <c>client_guid</c>: un reintento con carga idéntica devuelve 200; una creación
    /// nueva devuelve 201.
    /// </summary>
    /// <param name="request">Datos de la comida.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El resultado del registro.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMealRequest request, CancellationToken ct)
    {
        var clientGuid = ResolveClientGuid(request.ClientGuid);
        var command = new CreateMealCommand(clientGuid, request.MealTime, request.ConsumedAt, request.ClientCreatedAt, request.Items);
        var result = await _mediator.Send(command, ct);

        var statusCode = _idempotencyContext.WasReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created;
        return StatusCode(statusCode, result);
    }

    /// <summary>
    /// Devuelve el historial paginado de comidas del paciente autenticado.
    /// </summary>
    /// <param name="from">Inicio del rango (UTC).</param>
    /// <param name="to">Fin del rango (UTC).</param>
    /// <param name="page">Número de página.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El historial de comidas.</returns>
    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMealHistoryQuery(from, to, page, pageSize), ct);
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
