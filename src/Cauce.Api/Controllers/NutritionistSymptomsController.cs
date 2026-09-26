using Cauce.Api.Configuration;
using Cauce.Api.Contracts.ClinicalRegistry;
using Cauce.Application.ClinicalRegistry.UseCases.SetSymptomMealAssociation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints del nutricionista sobre los síntomas de sus pacientes asignados. Comparte el prefijo
/// <c>/symptoms</c> con <see cref="SymptomsController"/>, que exige el rol de paciente a nivel de clase;
/// por eso las acciones del nutricionista viven en un controlador aparte, como en recomendaciones.
/// </summary>
[Route("api/v{version:apiVersion}/symptoms")]
[Authorize(Policy = "Nutritionist")]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class NutritionistSymptomsController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con sus dependencias.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public NutritionistSymptomsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Corrige a mano la comida asociada a un síntoma de un paciente asignado: con <c>mealId</c> la fija a
    /// esa comida, sin sujeción a la ventana de 4 horas; con <c>mealId: null</c> la desvincula. La comida
    /// debe ser del mismo paciente. Requiere el header <c>Idempotency-Key</c>.
    /// </summary>
    /// <param name="id">Identificador del síntoma.</param>
    /// <param name="request">Comida que se asocia, o <see langword="null"/> para desvincular.</param>
    /// <param name="idempotencyKey">Clave de idempotencia del header <c>Idempotency-Key</c>.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Sin contenido.</returns>
    [HttpPut("{id:guid}/meal-association")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetMealAssociation(
        Guid id,
        [FromBody] SetSymptomMealAssociationRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken ct)
    {
        await _mediator.Send(new SetSymptomMealAssociationCommand(id, request.MealId, idempotencyKey), ct);
        return NoContent();
    }
}
