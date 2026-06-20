using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Cauce.Api.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Cauce.Api.Infrastructure.Services;

/// <summary>
/// Implementación de ICurrentUserService basada en IHttpContextAccessor.
/// Lee los claims emitidos por el middleware JwtBearer y los expone a la
/// capa Application sin acoplarla al pipeline HTTP.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Inicializa una nueva instancia de CurrentUserService.
    /// </summary>
    /// <param name="httpContextAccessor">Accesor del contexto HTTP actual.</param>
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public Guid GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("No hay un usuario autenticado en el contexto actual.");
        }

        // Busca primero el claim "sub" (emitido por AuthService durante la generación del JWT).
        // Si el middleware JwtBearer mapeó el claim a NameIdentifier, lo extrae desde ahí.
        var subClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                       ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(subClaim) || !Guid.TryParse(subClaim, out var userId))
        {
            throw new UnauthorizedAccessException(
                "El token JWT no contiene un identificador de usuario válido en el claim sub.");
        }

        return userId;
    }
}