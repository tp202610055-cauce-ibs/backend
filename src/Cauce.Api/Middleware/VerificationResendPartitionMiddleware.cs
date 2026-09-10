using System.Text.Json;

namespace Cauce.Api.Middleware;

/// <summary>
/// Extrae y normaliza el correo del cuerpo de <c>POST /auth/verification-email/resend</c> y lo deja en
/// <see cref="HttpContext.Items"/> para que la política de rate limit pueda particionar por él
/// (acta A44).
/// </summary>
/// <remarks>
/// Existe porque las fábricas de <c>RateLimitPartition</c> son síncronas y el cuerpo de la petición es
/// un stream de una sola lectura: no se puede leer desde la política sin recurrir a I/O síncrona. La
/// solución replica el patrón ya probado en <see cref="AuditingMiddleware"/>, que bufferiza el cuerpo
/// para leer el correo del login, con la diferencia de que este debe correr <b>antes</b> de
/// <c>UseRateLimiter</c>.
/// <para>
/// Solo actúa sobre esa ruta. Cualquier otra petición pasa sin que se toque su cuerpo.
/// </para>
/// </remarks>
public sealed class VerificationResendPartitionMiddleware
{
    /// <summary>
    /// Clave con la que el correo normalizado viaja en <see cref="HttpContext.Items"/>.
    /// </summary>
    public const string EmailItemKey = "cauce.verify-resend-email";

    private const string RoutePath = "/auth/verification-email/resend";

    private readonly RequestDelegate _next;

    /// <summary>
    /// Inicializa el middleware con el siguiente eslabón del pipeline.
    /// </summary>
    /// <param name="next">Siguiente middleware.</param>
    public VerificationResendPartitionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Procesa la petición, extrayendo el correo cuando corresponde.
    /// </summary>
    /// <param name="context">Contexto HTTP.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isResend = context.Request.Method == HttpMethods.Post
            && path.EndsWith(RoutePath, StringComparison.OrdinalIgnoreCase);

        if (isResend)
        {
            context.Request.EnableBuffering();
            var email = await TryReadNormalizedEmailAsync(context).ConfigureAwait(false);
            if (email is not null)
            {
                context.Items[EmailItemKey] = email;
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Lee el correo del cuerpo y lo normaliza a minúsculas sin espacios envolventes, para que
    /// variaciones de caja caigan en la misma partición. Rebobina el stream para no consumirlo.
    /// </summary>
    /// <param name="context">Contexto HTTP.</param>
    /// <returns>El correo normalizado, o <see langword="null"/> si el cuerpo no lo trae.</returns>
    private static async Task<string?> TryReadNormalizedEmailAsync(HttpContext context)
    {
        try
        {
            context.Request.Body.Position = 0;
            using var document = await JsonDocument
                .ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted)
                .ConfigureAwait(false);
            context.Request.Body.Position = 0;

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("email", out var emailElement)
                && emailElement.ValueKind == JsonValueKind.String)
            {
                var email = emailElement.GetString();
                return string.IsNullOrWhiteSpace(email)
                    ? null
                    : email.Trim().ToLowerInvariant();
            }
        }
        catch (JsonException)
        {
            // Cuerpo no JSON o malformado. La política cae a la IP de origen, que es más restrictivo
            // que una cubeta compartida: con una clave común, cuerpos inválidos agotarían el límite
            // de todos los usuarios.
            context.Request.Body.Position = 0;
        }

        return null;
    }
}

/// <summary>
/// Métodos de extensión para registrar <see cref="VerificationResendPartitionMiddleware"/>.
/// </summary>
public static class VerificationResendPartitionMiddlewareExtensions
{
    /// <summary>
    /// Añade al pipeline la extracción del correo para la partición del rate limit. Debe registrarse
    /// <b>antes</b> de <c>UseRateLimiter</c>.
    /// </summary>
    /// <param name="app">Constructor del pipeline de la aplicación.</param>
    /// <returns>El mismo constructor para encadenamiento.</returns>
    public static IApplicationBuilder UseVerificationResendPartition(this IApplicationBuilder app)
    {
        return app.UseMiddleware<VerificationResendPartitionMiddleware>();
    }
}
