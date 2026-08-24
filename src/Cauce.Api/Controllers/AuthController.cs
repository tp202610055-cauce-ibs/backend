using Cauce.Api.Configuration;
using Cauce.Api.Contracts.Identity;
using Cauce.Application.Identity.UseCases.ConfirmPasswordReset;
using Cauce.Application.Identity.UseCases.Login;
using Cauce.Application.Identity.UseCases.Logout;
using Cauce.Application.Identity.UseCases.RefreshToken;
using Cauce.Application.Identity.UseCases.RegisterPatient;
using Cauce.Application.Identity.UseCases.RequestPasswordReset;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints de autenticación e identidad: registro de pacientes, inicio y cierre de sesión
/// (passthrough a Keycloak) y restablecimiento de contraseña. El backend es la única puerta de
/// entrada, lo que permite auditar LOGIN/LOGOUT/FAILED_LOGIN en el middleware (acta A3).
/// </summary>
[Route("api/v{version:apiVersion}/auth")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType(typeof(RegisterPatientResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
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
    /// Inicia sesión haciendo passthrough a Keycloak y devuelve los tokens emitidos. Ante
    /// credenciales inválidas responde 401 con un mensaje genérico (no revela si la cuenta existe).
    /// </summary>
    /// <param name="request">Credenciales y cliente OIDC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>200 con los tokens.</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password, request.ClientId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Renueva la sesión a partir de un refresh token vigente. Devuelve un juego de tokens nuevo,
    /// incluida la identidad del usuario. El realm rota los refresh tokens: el enviado aquí queda
    /// revocado y el cliente debe persistir el que recibe.
    /// </summary>
    /// <param name="request">Refresh token y cliente OIDC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>200 con los tokens renovados.</returns>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitingPolicies.AuthRefresh)]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(request.RefreshToken, request.ClientId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Cierra sesión revocando el refresh token en Keycloak.
    /// </summary>
    /// <param name="request">Refresh token y cliente OIDC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>204 si se procesó la revocación.</returns>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        await _mediator.Send(new LogoutCommand(request.RefreshToken, request.ClientId), ct);
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestPasswordReset(
        [FromBody] RequestPasswordResetRequest request,
        CancellationToken ct)
    {
        var command = new RequestPasswordResetCommand(
            request.Email,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            request.ClientId);

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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
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
