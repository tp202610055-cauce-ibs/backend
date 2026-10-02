using System.Security.Claims;
using System.Text.Json;
using Cauce.Api.Authorization;
using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using Cauce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.Middleware;

/// <summary>
/// Middleware de auditoría de la capa HTTP (capa 1 de DEC-B5-01): registra los eventos de
/// autenticación (LOGIN, FAILED_LOGIN, ACCOUNT_LOCKED, LOGOUT, FAILED_LOGOUT) que no corresponden a un
/// cambio en una tabla con trigger. Cubre las rutas del móvil y las del portal, y guarda en el contexto
/// adicional el canal y, si hubo rechazo, su causa interna (acta A68). Escribe en su propio
/// <see cref="IServiceScope"/> con su propio <c>SaveChanges</c>, para no acoplarse a la transacción del
/// endpoint. No registra credenciales.
/// </summary>
public sealed class AuditingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditingMiddleware> _logger;

    /// <summary>
    /// Inicializa el middleware con sus dependencias.
    /// </summary>
    /// <param name="next">Siguiente delegado del pipeline.</param>
    /// <param name="scopeFactory">Fábrica de ámbitos de servicio.</param>
    /// <param name="logger">Logger de la categoría del middleware.</param>
    public AuditingMiddleware(
        RequestDelegate next,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditingMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el middleware: clasifica el evento de autenticación por ruta y estado, y lo audita.
    /// </summary>
    /// <param name="context">Contexto HTTP de la petición.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var sessionEvent = ClassifyRoute(context.Request);
        if (sessionEvent is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var (kind, channel) = sessionEvent.Value;
        string? loginEmail = null;
        if (kind == SessionEventKind.Login)
        {
            context.Request.EnableBuffering();
            loginEmail = await TryReadEmailAsync(context).ConfigureAwait(false);
        }

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Un intento que lanza (por ejemplo, credenciales inválidas) igual debe auditarse antes de que
            // la excepción llegue al middleware de manejo de errores (acta A14).
            await SafeAuditAsync(() => AuditAsync(context, kind, channel, loginEmail, exception))
                .ConfigureAwait(false);
            throw;
        }

        await SafeAuditAsync(() => AuditAsync(context, kind, channel, loginEmail, exception: null))
            .ConfigureAwait(false);
    }

    private static (SessionEventKind Kind, LoginChannel Channel)? ClassifyRoute(HttpRequest request)
    {
        if (request.Method != HttpMethods.Post)
        {
            return null;
        }

        var path = request.Path.Value ?? string.Empty;
        return path switch
        {
            _ when path.EndsWith("/auth/portal/login", StringComparison.OrdinalIgnoreCase)
                => (SessionEventKind.Login, LoginChannel.Portal),
            _ when path.EndsWith("/auth/portal/logout", StringComparison.OrdinalIgnoreCase)
                => (SessionEventKind.Logout, LoginChannel.Portal),
            _ when path.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase)
                => (SessionEventKind.Login, LoginChannel.Mobile),
            _ when path.EndsWith("/auth/logout", StringComparison.OrdinalIgnoreCase)
                => (SessionEventKind.Logout, LoginChannel.Mobile),
            _ => null
        };
    }

    private async Task SafeAuditAsync(Func<Task> audit)
    {
        try
        {
            await audit().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // La auditoría de autenticación no debe romper la respuesta ni enmascarar la excepción original.
            _logger.LogError(exception, "Failed to write authentication audit log.");
        }
    }

    private async Task AuditAsync(
        HttpContext context,
        SessionEventKind kind,
        LoginChannel channel,
        string? loginEmail,
        Exception? exception)
    {
        var succeeded = exception is null && context.Response.StatusCode is >= 200 and < 300;
        var cause = succeeded ? null : ResolveCause(context, exception);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        var actorId = kind == SessionEventKind.Login
            ? await ResolveActorByEmailAsync(dbContext, loginEmail, context.RequestAborted).ConfigureAwait(false)
            : await ResolveActorByPrincipalAsync(dbContext, context.User, context.RequestAborted).ConfigureAwait(false);

        var actionType = (kind, succeeded) switch
        {
            (SessionEventKind.Login, true) => AuditActionType.Login,
            (SessionEventKind.Login, false) when cause == AuthFailureCauses.AccountLocked
                => AuditActionType.AccountLocked,
            (SessionEventKind.Login, false) => AuditActionType.FailedLogin,
            (SessionEventKind.Logout, true) => AuditActionType.Logout,
            _ => AuditActionType.FailedLogout
        };

        if (actionType == AuditActionType.AccountLocked)
        {
            // CP015 pide el bloqueo como alerta de seguridad: además de la fila, un evento de nivel Warning
            // que el monitoreo puede filtrar sin leer la base.
            _logger.LogWarning(
                "SecurityEvent account_locked: login attempt on a locked account (actor {ActorId}, channel {Channel}).",
                actorId,
                AuthAuditContext.ChannelName(channel));
        }

        var auditLog = AuditLog.Record(
            actorId,
            actionType,
            nameof(Cauce.Domain.Identity.User),
            entityId: null,
            oldValuesHash: null,
            newValuesHash: null,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null,
            additionalContext: AuthAuditContext.Build(channel, cause),
            occurredAtUtc: DateTime.UtcNow);

        await dbContext.AuditLogs.AddAsync(auditLog, context.RequestAborted).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>
    /// Determina la causa interna de un intento rechazado. Prefiere la que registró el handler, que es la
    /// única que conoce el estado de la cuenta; si el rechazo ocurrió antes de llegar al handler, la
    /// deduce de la excepción o del código de estado.
    /// </summary>
    /// <param name="context">Contexto HTTP de la petición.</param>
    /// <param name="exception">Excepción del intento, si la hubo.</param>
    /// <returns>Una de las causas de <see cref="AuthFailureCauses"/>.</returns>
    private static string ResolveCause(HttpContext context, Exception? exception)
    {
        var attemptContext = context.RequestServices.GetService<IAuthenticationAttemptContext>();
        if (attemptContext?.FailureCause is { } recorded)
        {
            return recorded;
        }

        return exception switch
        {
            AccountLockedException => AuthFailureCauses.AccountLocked,
            ValidationException => AuthFailureCauses.InvalidRequest,
            UnsupportedOidcClientException => AuthFailureCauses.UnsupportedClient,
            IdentityProviderMisconfiguredException => AuthFailureCauses.ClientMisconfigured,
            PortalCsrfHeaderMissingException => AuthFailureCauses.CsrfHeaderMissing,
            null when context.Response.StatusCode == StatusCodes.Status400BadRequest
                => AuthFailureCauses.InvalidRequest,
            _ => AuthFailureCauses.Undetermined
        };
    }

    private static async Task<Guid?> ResolveActorByEmailAsync(
        CauceDbContext dbContext,
        string? email,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        // El mismo criterio con que se guarda el correo, para que un login con otra capitalización no
        // quede auditado sin actor (acta A70).
        var normalizedEmail = EmailNormalization.Normalize(email);
        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Email == normalizedEmail)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    private static async Task<Guid?> ResolveActorByPrincipalAsync(
        CauceDbContext dbContext,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        var keycloakSubject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(keycloakSubject))
        {
            return null;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.KeycloakId == keycloakSubject)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    private static async Task<string?> TryReadEmailAsync(HttpContext context)
    {
        try
        {
            context.Request.Body.Position = 0;
            using var document = await JsonDocument
                .ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted)
                .ConfigureAwait(false);
            context.Request.Body.Position = 0;

            if (document.RootElement.TryGetProperty("email", out var emailElement)
                && emailElement.ValueKind == JsonValueKind.String)
            {
                return emailElement.GetString();
            }
        }
        catch (JsonException)
        {
            // Cuerpo no JSON o malformado: se audita el intento sin actor.
            context.Request.Body.Position = 0;
        }

        return null;
    }

    private enum SessionEventKind
    {
        Login,
        Logout
    }
}

/// <summary>
/// Métodos de extensión para registrar <see cref="AuditingMiddleware"/> en el pipeline.
/// </summary>
public static class AuditingMiddlewareExtensions
{
    /// <summary>
    /// Agrega el middleware de auditoría de autenticación al pipeline.
    /// </summary>
    /// <param name="app">Constructor de la aplicación.</param>
    /// <returns>El mismo constructor, para encadenar llamadas.</returns>
    public static IApplicationBuilder UseAuditingMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditingMiddleware>();
    }
}
