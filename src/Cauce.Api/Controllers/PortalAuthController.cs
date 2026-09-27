using Cauce.Api.Authorization;
using Cauce.Api.Configuration;
using Cauce.Api.Contracts.Identity;
using Cauce.Application.Common.Identity;
using Cauce.Application.Identity.UseCases.Login;
using Cauce.Application.Identity.UseCases.Logout;
using Cauce.Application.Identity.UseCases.RefreshToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Cauce.Api.Controllers;

/// <summary>
/// Sesión del portal web de nutricionistas (acta A68). El portal se autentica contra el backend, como el
/// móvil, y nunca habla con Keycloak: el backend pide los tokens con el cliente confidencial
/// <c>cauce-web-portal</c>. Solo entra el rol nutricionista. El refresh token viaja en la cookie
/// <c>HttpOnly</c> <c>cauce_portal_rt</c>, nunca en el cuerpo, y la renovación y el cierre exigen además el
/// header <c>X-Cauce-Portal</c> como defensa contra CSRF.
/// </summary>
[Route("api/v{version:apiVersion}/auth/portal")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class PortalAuthController : BaseApiController
{
    private readonly ISender _mediator;
    private readonly PortalSessionOptions _options;

    /// <summary>
    /// Inicializa el controlador con sus dependencias.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    /// <param name="options">Opciones de la sesión del portal.</param>
    public PortalAuthController(ISender mediator, IOptions<PortalSessionOptions> options)
    {
        _mediator = mediator;
        _options = options.Value;
    }

    /// <summary>
    /// Inicia sesión en el portal. Devuelve el access token en el cuerpo y deja el refresh token en la
    /// cookie <c>cauce_portal_rt</c>. Un paciente, una cuenta deshabilitada, pendiente de activación,
    /// suspendida o inactiva, y una contraseña incorrecta reciben el mismo 401 <c>invalid_credentials</c>;
    /// la causa queda solo en la auditoría. La cuenta bloqueada por intentos fallidos recibe 423
    /// <c>account_locked</c> con <c>lockedUntil</c>.
    /// </summary>
    /// <param name="request">Credenciales del nutricionista.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El access token y la identidad del nutricionista.</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingPolicies.AuthPortalLogin)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(PortalSessionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] PortalLoginRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new LoginCommand(request.Email, request.Password, OidcClients.WebPortal, LoginChannel.Portal),
            ct);
        return Ok(StartSession(result));
    }

    /// <summary>
    /// Renueva la sesión del portal con el refresh token de la cookie. Exige el header
    /// <c>X-Cauce-Portal</c>. Rota la cookie y devuelve un access token nuevo. Sin cookie, o con un token
    /// vencido, revocado o de un nutricionista que ya no puede usar el portal, responde 401
    /// <c>invalid_refresh_token</c>: el portal debe volver al login.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El access token nuevo y la identidad del nutricionista.</returns>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [RequirePortalCsrfHeader]
    [EnableRateLimiting(RateLimitingPolicies.AuthPortalRefresh)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(PortalSessionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RefreshTokenCommand(ReadRefreshCookie(), OidcClients.WebPortal, LoginChannel.Portal),
            ct);
        return Ok(StartSession(result));
    }

    /// <summary>
    /// Cierra la sesión del portal: revoca en Keycloak el refresh token de la cookie y la borra. Exige el
    /// access token del nutricionista y el header <c>X-Cauce-Portal</c>. Sin cookie, igual responde 204.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Sin contenido.</returns>
    [Authorize(Policy = "Nutritionist")]
    [HttpPost("logout")]
    [RequirePortalCsrfHeader]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _mediator.Send(
            new LogoutCommand(ReadRefreshCookie(), OidcClients.WebPortal, LoginChannel.Portal),
            ct);

        Response.Cookies.Delete(PortalSessionOptions.CookieName, BuildCookieOptions(maxAge: null));
        return NoContent();
    }

    private PortalSessionResult StartSession(LoginResult result)
    {
        Response.Cookies.Append(
            PortalSessionOptions.CookieName,
            result.RefreshToken,
            BuildCookieOptions(TimeSpan.FromSeconds(result.RefreshExpiresIn)));

        return new PortalSessionResult(result.AccessToken, result.ExpiresIn, result.TokenType, result.User);
    }

    private string ReadRefreshCookie()
    {
        return Request.Cookies.TryGetValue(PortalSessionOptions.CookieName, out var value) ? value : string.Empty;
    }

    /// <summary>
    /// Arma los atributos de la cookie del refresh token. La vigencia coincide con la del refresh token:
    /// el navegador la descarta cuando Keycloak ya no la aceptaría.
    /// </summary>
    /// <param name="maxAge">Vigencia de la cookie, o <see langword="null"/> para borrarla.</param>
    /// <returns>Los atributos de la cookie.</returns>
    private CookieOptions BuildCookieOptions(TimeSpan? maxAge)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = _options.CookieSecure,
            SameSite = SameSiteMode.Strict,
            Path = PortalSessionOptions.CookiePath,
            MaxAge = maxAge,
            IsEssential = true
        };
    }
}
