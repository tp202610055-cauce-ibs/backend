using Cauce.Api.Configuration;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cauce.Api.Authorization;

/// <summary>
/// Filtro que exige el header <see cref="PortalSessionOptions.CsrfHeaderName"/> con un valor no vacío en
/// las rutas del portal que leen la cookie del refresh token (acta A68). Corre antes del model binding, así
/// que una petición sin el header no llega al handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequirePortalCsrfHeaderAttribute : Attribute, IAuthorizationFilter
{
    /// <inheritdoc />
    /// <exception cref="PortalCsrfHeaderMissingException">Si el header no está o viene vacío.</exception>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(PortalSessionOptions.CsrfHeaderName, out var values)
            || string.IsNullOrWhiteSpace(values.ToString()))
        {
            throw new PortalCsrfHeaderMissingException();
        }
    }
}
