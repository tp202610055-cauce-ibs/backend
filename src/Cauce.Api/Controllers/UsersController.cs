using Cauce.Api.Application.DTOs.Users;
using Cauce.Api.Application.Exceptions;
using Cauce.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Controlador de operaciones relacionadas al usuario autenticado.
/// Todos los endpoints requieren un JWT válido emitido por AuthController.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UsersController> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de UsersController.
    /// </summary>
    public UsersController(
        IUserService userService,
        ICurrentUserService currentUserService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el perfil del usuario autenticado actualmente.
    /// </summary>
    /// <returns>Perfil del usuario sin datos sensibles.</returns>
    /// <response code="200">Perfil retornado correctamente.</response>
    /// <response code="401">Token JWT ausente, inválido o expirado.</response>
    /// <response code="404">El usuario asociado al token ya no existe en la base de datos.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfileAsync()
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var profile = await _userService.GetProfileAsync(userId);

            _logger.LogInformation(
                "Perfil consultado correctamente para usuario {UserId}", userId);

            return Ok(profile);
        }
        catch (UserNotFoundException ex)
        {
            _logger.LogWarning(
                "Usuario autenticado no encontrado en base de datos. {Message}",
                ex.Message);

            return NotFound(new
            {
                error = "user_not_found",
                message = "El usuario asociado al token ya no existe."
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Acceso no autorizado. {Message}", ex.Message);

            return Unauthorized(new
            {
                error = "unauthorized",
                message = ex.Message
            });
        }
    }
}