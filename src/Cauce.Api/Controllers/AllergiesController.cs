using Cauce.Application.Patients.UseCases.ListAllergiesCatalog;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoint del catálogo de alergias, accesible por cualquier usuario autenticado.
/// </summary>
[Route("api/v{version:apiVersion}/allergies")]
[Authorize]
public sealed class AllergiesController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public AllergiesController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista el catálogo de alergias activas.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El catálogo de alergias activas.</returns>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListAllergiesCatalogQuery(), ct);
        return Ok(result);
    }
}
