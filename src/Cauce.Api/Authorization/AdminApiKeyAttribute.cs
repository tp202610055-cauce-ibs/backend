using System.Security.Cryptography;
using System.Text;
using Cauce.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Cauce.Api.Authorization;

/// <summary>
/// Filtro de autorización que exige el header <c>X-Admin-Api-Key</c> con el valor
/// configurado en <see cref="AdminApiKeyOptions"/>. La comparación se realiza en
/// tiempo constante para mitigar ataques de temporización.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const string HeaderName = "X-Admin-Api-Key";

    /// <inheritdoc />
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var options = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<AdminApiKeyOptions>>().Value;

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var providedValues)
            || !FixedTimeEquals(providedValues.ToString(), options.Value))
        {
            context.Result = new UnauthorizedResult();
        }

        return Task.CompletedTask;
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
