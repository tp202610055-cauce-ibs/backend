using Cauce.Api.Configuration;
using Cauce.Api.Contracts.ClinicalRegistry;
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
    public async Task<IActionResult> Create([FromBody] CreateCustomFoodRequest request, CancellationToken ct)
    {
        var command = new CreateCustomFoodCommand(request.Name, request.PortionSizeGrams, request.Ingredients);
        var result = await _mediator.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Lista los alimentos personalizados del paciente autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los alimentos personalizados.</returns>
    [HttpGet]
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
    public async Task<IActionResult> Delete(Guid customFoodId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteCustomFoodCommand(customFoodId), ct);
        return NoContent();
    }
}
