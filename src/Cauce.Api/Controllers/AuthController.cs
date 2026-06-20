using Cauce.Api.Application.DTOs.Requests;
using Cauce.Api.Application.DTOs.Responses;
using Cauce.Api.Application.Exceptions;
using Cauce.Api.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints de autenticación. Implementa US01 (registro de paciente) y
/// US05 (inicio de sesión del paciente) del Product Backlog v5.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Registra un nuevo paciente y emite un token JWT inicial (US01).
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        // US01 CA03: bloqueo sin aceptación del consentimiento informado.
        if (!request.ConsentAccepted)
        {
            return BadRequest(new
            {
                error = "El consentimiento informado es obligatorio para crear una cuenta.",
                field = nameof(request.ConsentAccepted)
            });
        }

        try
        {
            var response = await _authService.RegisterPatientAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (DuplicateEmailException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (WeakPasswordException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }

    /// <summary>
    /// Autentica a un usuario existente y emite un token JWT (US05).
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        try
        {
            var response = await _authService.LoginAsync(request, ct);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (AccountLockedException ex)
        {
            return StatusCode(StatusCodes.Status423Locked, new
            {
                error = ex.Message,
                lockedUntil = ex.LockedUntil
            });
        }
    }
}