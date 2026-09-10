using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Cauce.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Configuration;

/// <summary>
/// Configuración centralizada de las políticas de limitación de tasa (rate
/// limiting) definidas en la decisión DEC-B3-03. Cada política usa una ventana
/// fija particionada por IP de origen (endpoints públicos de autenticación) o por
/// el claim <c>sub</c> del usuario autenticado (endpoints con sesión).
/// </summary>
public static class RateLimitingPolicies
{
    /// <summary>
    /// Política para el registro de pacientes: 5 peticiones por hora por IP.
    /// </summary>
    public const string AuthRegister = "auth-register";

    /// <summary>
    /// Política para el inicio de sesión: 10 peticiones por minuto por IP.
    /// </summary>
    public const string AuthLogin = "auth-login";

    /// <summary>
    /// Política para la solicitud de restablecimiento de contraseña: 3 peticiones
    /// por hora por IP.
    /// </summary>
    public const string AuthPasswordReset = "auth-pwreset";

    /// <summary>
    /// Política por defecto para endpoints autenticados: 60 peticiones por minuto
    /// por usuario.
    /// </summary>
    public const string DefaultAuthenticated = "default-auth";

    /// <summary>
    /// Política para endpoints de sincronización: 120 peticiones por minuto por usuario.
    /// </summary>
    public const string Sync = "sync";

    /// <summary>
    /// Política para la consulta del consentimiento vigente: 60 peticiones por minuto
    /// por IP. Es un endpoint anónimo que el cliente consulta durante el registro.
    /// </summary>
    public const string ConsentCurrent = "consent-current";

    /// <summary>
    /// Política para la renovación de sesión: 20 peticiones por minuto por IP. Un cliente bien
    /// portado renueva cerca de la expiración del access token (900 s), o sea cuatro veces por hora;
    /// el margen absorbe ráfagas y varios dispositivos tras una misma IP compartida.
    /// </summary>
    public const string AuthRefresh = "auth-refresh";

    /// <summary>
    /// Política para el reenvío del correo de verificación: 3 peticiones por hora, particionadas por el
    /// correo normalizado del cuerpo y no por IP (acta A40, decisión D3). Particionar por IP dejaría que
    /// un usuario legítimo tras una NAT compartida agotara el cupo de sus vecinos, y no frenaría a quien
    /// hostiga un mismo buzón desde direcciones distintas.
    /// </summary>
    public const string AuthVerifyResend = "auth-verify-resend";

    /// <summary>
    /// Registra las ocho políticas de limitación de tasa y el comportamiento de
    /// rechazo (respuesta 429 con detalle de problema RFC 7807).
    /// </summary>
    /// <param name="options">Opciones del limitador de tasa a configurar.</param>
    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        AddIpFixedWindow(options, AuthRegister, permitLimit: 5, window: TimeSpan.FromHours(1));
        AddIpFixedWindow(options, AuthLogin, permitLimit: 10, window: TimeSpan.FromMinutes(1));
        AddIpFixedWindow(options, AuthPasswordReset, permitLimit: 3, window: TimeSpan.FromHours(1));
        AddIpFixedWindow(options, ConsentCurrent, permitLimit: 60, window: TimeSpan.FromMinutes(1));
        AddIpFixedWindow(options, AuthRefresh, permitLimit: 20, window: TimeSpan.FromMinutes(1));
        AddEmailFixedWindow(options, AuthVerifyResend, permitLimit: 3, window: TimeSpan.FromHours(1));
        AddUserFixedWindow(options, DefaultAuthenticated, permitLimit: 60, window: TimeSpan.FromMinutes(1));
        AddUserFixedWindow(options, Sync, permitLimit: 120, window: TimeSpan.FromMinutes(1));

        options.OnRejected = async (context, cancellationToken) =>
        {
            var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? (int)retryAfter.TotalSeconds
                : 0;

            if (retryAfterSeconds > 0)
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            }

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Demasiadas solicitudes",
                Detail = "Se superó el límite de solicitudes permitidas. Intente nuevamente más tarde."
            };
            problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/problem+json";
            await context.HttpContext.Response
                .WriteAsJsonAsync(problem, problem.GetType(), cancellationToken)
                .ConfigureAwait(false);
        };
    }

    private static void AddIpFixedWindow(
        RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        options.AddPolicy(policyName, httpContext =>
        {
            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window
                });
        });
    }

    /// <summary>
    /// Registra una política particionada por el correo normalizado que
    /// <see cref="VerificationResendPartitionMiddleware"/> dejó en <see cref="HttpContext.Items"/>.
    /// </summary>
    /// <param name="options">Opciones del limitador de tasa.</param>
    /// <param name="policyName">Nombre de la política.</param>
    /// <param name="permitLimit">Peticiones permitidas por ventana.</param>
    /// <param name="window">Duración de la ventana fija.</param>
    private static void AddEmailFixedWindow(
        RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        options.AddPolicy(policyName, httpContext =>
        {
            // Sin correo utilizable (cuerpo malformado o ausente) se cae a la IP de origen. Una clave
            // compartida permitiría agotar el cupo de todos enviando cuerpos inválidos.
            var email = httpContext.Items.TryGetValue(VerificationResendPartitionMiddleware.EmailItemKey, out var value)
                ? value as string
                : null;

            var partitionKey = email
                ?? $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window
                });
        });
    }

    private static void AddUserFixedWindow(
        RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        options.AddPolicy(policyName, httpContext =>
        {
            var partitionKey =
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub")
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window
                });
        });
    }
}
