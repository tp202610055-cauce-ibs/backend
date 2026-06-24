using System.Security.Claims;
using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="ICurrentUserService"/> que extrae la información
/// del usuario autenticado a partir de los claims del JWT y del contexto HTTP.
/// Si no hay contexto HTTP disponible (por ejemplo, en un worker en segundo plano),
/// las propiedades devuelven <see langword="null"/> y la autenticación es falsa.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private const string RealmAccessClaimType = "realm_access";

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Inicializa el servicio con el accesor del contexto HTTP.
    /// </summary>
    /// <param name="httpContextAccessor">Accesor del contexto HTTP actual.</param>
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid? UserId
    {
        get
        {
            var subject = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value;

            return Guid.TryParse(subject, out var userId) ? userId : null;
        }
    }

    /// <inheritdoc />
    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst("email")?.Value;

    /// <inheritdoc />
    public IReadOnlyList<string> Roles => ExtractRealmRoles();

    /// <inheritdoc />
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <inheritdoc />
    public string? UserAgent
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return null;
            }

            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            return string.IsNullOrEmpty(userAgent) ? null : userAgent;
        }
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// Extrae los roles del realm desde el claim <c>realm_access</c>, que Keycloak
    /// emite como un objeto JSON con un arreglo <c>roles</c>.
    /// </summary>
    /// <returns>Lista de roles del realm; vacía si no hay claim o es inválido.</returns>
    private IReadOnlyList<string> ExtractRealmRoles()
    {
        var realmAccess = User?.FindFirst(RealmAccessClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(realmAccess);
            if (!document.RootElement.TryGetProperty("roles", out var rolesElement)
                || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var roles = new List<string>(rolesElement.GetArrayLength());
            foreach (var role in rolesElement.EnumerateArray())
            {
                var value = role.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    roles.Add(value);
                }
            }

            return roles;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
