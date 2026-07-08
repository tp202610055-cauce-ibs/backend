using Cauce.Api.Contracts.Identity;
using Cauce.Application.Identity.UseCases.UpdateFcmToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints de la cuenta del usuario autenticado, transversales a los roles paciente y nutricionista.
/// </summary>
[Route("api/v{version:apiVersion}/users")]
[Authorize]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class UsersController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public UsersController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registra o actualiza el token de notificaciones push (Firebase Cloud Messaging) del dispositivo
    /// del usuario autenticado (TS10 CA01). Enviar <c>null</c> desvincula el token.
    /// </summary>
    /// <param name="request">Cuerpo con el token de FCM.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>204 si se registró correctamente.</returns>
    [HttpPut("me/fcm-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateFcmTokenCommand(request.FcmToken), ct);
        return NoContent();
    }
}
