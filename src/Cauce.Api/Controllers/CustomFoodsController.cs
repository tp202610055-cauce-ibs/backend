using Cauce.Api.Configuration;
using Cauce.Api.Contracts.ClinicalRegistry;
using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.UseCases.CreateCustomFood;
using Cauce.Application.ClinicalRegistry.UseCases.DeleteCustomFood;
using Cauce.Application.ClinicalRegistry.UseCases.ListCustomFoods;
using Cauce.Application.ClinicalRegistry.UseCases.UpdateCustomFood;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints del paciente para gestionar sus alimentos personalizados.
/// </summary>
[Route("api/v{version:apiVersion}/custom-foods")]
[Authorize(Policy = "Patient")]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class CustomFoodsController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public CustomFoodsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Crea un alimento personalizado del paciente autenticado.
    /// </summary>
    /// <param name="request">Datos del alimento personalizado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador del alimento creado, con código 201.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CreateCustomFoodResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCustomFoodRequest request, CancellationToken ct)
    {
        var command = new CreateCustomFoodCommand(
            request.Name, request.PortionSizeGrams, request.Ingredients, request.ConfirmedAllergens);
        var result = await _mediator.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Lista los alimentos personalizados del paciente autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los alimentos personalizados.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomFoodSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListCustomFoodsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Actualiza un alimento personalizado del paciente autenticado.
    /// </summary>
    /// <param name="customFoodId">Identificador del alimento personalizado.</param>
    /// <param name="request">Campos a actualizar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador del alimento actualizado.</returns>
    [HttpPut("{customFoodId:guid}")]
    [ProducesResponseType(typeof(UpdateCustomFoodResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid customFoodId, [FromBody] UpdateCustomFoodRequest request, CancellationToken ct)
    {
        var command = new UpdateCustomFoodCommand(customFoodId, request.Name, request.PortionSizeGrams, request.Ingredients);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Elimina un alimento personalizado del paciente autenticado.
    /// </summary>
    /// <param name="customFoodId">Identificador del alimento personalizado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>204 si se eliminó correctamente.</returns>
    [HttpDelete("{customFoodId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid customFoodId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteCustomFoodCommand(customFoodId), ct);
        return NoContent();
    }
}
