using Cauce.Api.Contracts.Identity;
using Cauce.Application.Identity.UseCases.ConfirmPasswordReset;
using Cauce.Application.Identity.UseCases.RegisterLogoutEvent;
using Cauce.Application.Identity.UseCases.RegisterLoginEvent;
using Cauce.Application.Identity.UseCases.RegisterPatient;
using Cauce.Application.Identity.UseCases.RequestPasswordReset;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints de autenticación e identidad: registro de pacientes, registro de
/// eventos de sesión y restablecimiento de contraseña. El inicio de sesión en sí
/// se realiza mediante OIDC entre el cliente y Keycloak, no por este controlador.
/// </summary>
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public AuthController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registra un nuevo paciente. El cliente puede enviar opcionalmente el header
    /// <c>Idempotency-Key</c> (UUID v4) para compatibilidad futura.
    /// </summary>
    /// <param name="request">Datos del registro.</param>
    /// <param name="idempotencyKey">Clave de idempotencia opcional.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El resultado del registro con código 201.</returns>
    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting("auth-register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPatientRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!IsValidIdempotencyKey(idempotencyKey))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key inválida",
                detail: "El header Idempotency-Key, si se envía, debe ser un UUID v4 válido.");
        }

        var command = new RegisterPatientCommand(
            request.Email,
            request.FullName,
            request.Password,
            request.ConsentDocumentVersion,
            request.ConsentTextHash,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            request.InvitationCode);

        var result = await _mediator.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Registra el inicio de sesión del usuario autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>204 si se registró correctamente.</returns>
    [Authorize]
    [HttpPost("sessions")]
    public async Task<IActionResult> RegisterSessionStart(CancellationToken ct)
    {
        await _mediator.Send(new RegisterLoginEventCommand(), ct);
        return NoContent();
    }

    /// <summary>
    /// Registra el cierre de sesión del usuario autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>204 si se registró correctamente.</returns>
    [Authorize]
    [HttpDelete("sessions")]
    public async Task<IActionResult> RegisterSessionEnd(CancellationToken ct)
    {
        await _mediator.Send(new RegisterLogoutEventCommand(), ct);
        return NoContent();
    }

    /// <summary>
    /// Solicita el restablecimiento de contraseña. Responde 200 exista o no la cuenta.
    /// </summary>
    /// <param name="request">Datos de la solicitud.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>200 siempre.</returns>
    [AllowAnonymous]
    [HttpPost("password-reset/request")]
    [EnableRateLimiting("auth-pwreset")]
    public async Task<IActionResult> RequestPasswordReset(
        [FromBody] RequestPasswordResetRequest request,
        CancellationToken ct)
    {
        var command = new RequestPasswordResetCommand(
            request.Email,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        await _mediator.Send(command, ct);
        return Ok();
    }

    /// <summary>
    /// Confirma el restablecimiento de contraseña con el token recibido.
    /// </summary>
    /// <param name="request">Datos de la confirmación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>200 si se restableció correctamente.</returns>
    [AllowAnonymous]
    [HttpPost("password-reset/confirm")]
    [EnableRateLimiting("auth-pwreset")]
    public async Task<IActionResult> ConfirmPasswordReset(
        [FromBody] ConfirmPasswordResetRequest request,
        CancellationToken ct)
    {
        var command = new ConfirmPasswordResetCommand(request.Token, request.NewPassword);
        await _mediator.Send(command, ct);
        return Ok();
    }

    private static bool IsValidIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return true;
        }

        return Guid.TryParse(idempotencyKey, out var parsed)
            && parsed.ToString().Length == 36
            && char.ToLowerInvariant(parsed.ToString()[14]) == '4';
    }
}
