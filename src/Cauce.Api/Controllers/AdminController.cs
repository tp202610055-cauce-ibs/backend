using Cauce.Api.Authorization;
using Cauce.Api.Contracts.Identity;
using Cauce.Application.Identity.UseCases.CreateNutritionist;
using Cauce.Application.Identity.UseCases.ResendNutritionistActivationEmail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints administrativos protegidos por clave de API. Permiten provisionar
/// nutricionistas durante el piloto sin exponer un flujo de OIDC con rol admin.
/// </summary>
[Route("api/v{version:apiVersion}/admin")]
[AllowAnonymous]
[AdminApiKey]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class AdminController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public AdminController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Provisiona un nuevo nutricionista pendiente de activación y pide a Keycloak que le envíe el enlace
    /// para definir su contraseña (acta A52).
    /// </summary>
    /// <param name="request">Datos del nutricionista.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El resultado de la provisión, con código 201.</returns>
    [HttpPost("nutritionists")]
    [ProducesResponseType(typeof(CreateNutritionistResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateNutritionist(
        [FromBody] CreateNutritionistRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateNutritionistCommand(request.Email, request.FullName), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Vuelve a enviar a un nutricionista pendiente el enlace para definir su contraseña (acta A52). Sirve
    /// cuando el enlace venció, porque dura 12 horas, o cuando el envío falló al provisionar la cuenta.
    /// </summary>
    /// <param name="id">Identificador local del nutricionista.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>204 cuando Keycloak aceptó el envío.</returns>
    [HttpPost("nutritionists/{id:guid}/activation-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ResendActivationEmail(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ResendNutritionistActivationEmailCommand(id), ct);
        return NoContent();
    }
}
