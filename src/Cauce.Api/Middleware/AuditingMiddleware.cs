using System.Security.Claims;
using System.Text.Json;
using Cauce.Domain.Auditing;
using Cauce.Domain.Auditing.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.Middleware;

/// <summary>
/// Middleware de auditoría de la capa HTTP (capa 1 de DEC-B5-01): registra los eventos de
/// autenticación (LOGIN, FAILED_LOGIN, LOGOUT) que no corresponden a un cambio en una tabla con
/// trigger. Escribe en su propio <see cref="IServiceScope"/> con su propio <c>SaveChanges</c>, para
/// no acoplarse a la transacción del endpoint. No registra credenciales.
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
    /// <param name="context">Contexto HTTP.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isLogin = context.Request.Method == HttpMethods.Post && path.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase);
        var isLogout = context.Request.Method == HttpMethods.Post && path.EndsWith("/auth/logout", StringComparison.OrdinalIgnoreCase);

        string? loginEmail = null;
        if (isLogin)
        {
            context.Request.EnableBuffering();
            loginEmail = await TryReadEmailAsync(context).ConfigureAwait(false);
        }

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch when (isLogin)
        {
            // Un login que lanza (por ejemplo, credenciales inválidas) igual debe auditarse como
            // FAILED_LOGIN antes de que la excepción llegue al middleware de manejo de errores (acta A14).
            await SafeAuditAsync(() => AuditLoginAsync(context, loginEmail, success: false)).ConfigureAwait(false);
            throw;
        }

        if (isLogin)
        {
            var success = context.Response.StatusCode is >= 200 and < 300;
            await SafeAuditAsync(() => AuditLoginAsync(context, loginEmail, success)).ConfigureAwait(false);
        }
        else if (isLogout && context.Response.StatusCode is >= 200 and < 300)
        {
            await SafeAuditAsync(() => AuditLogoutAsync(context)).ConfigureAwait(false);
        }
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

    private async Task AuditLoginAsync(HttpContext context, string? email, bool success)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        Guid? actorId = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            actorId = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Email == email)
                .Select(user => (Guid?)user.Id)
                .FirstOrDefaultAsync(context.RequestAborted)
                .ConfigureAwait(false);
        }

        var actionType = success ? AuditActionType.Login : AuditActionType.FailedLogin;
        await WriteAuditAsync(dbContext, actorId, actionType, context).ConfigureAwait(false);
    }

    private async Task AuditLogoutAsync(HttpContext context)
    {
        var keycloakSubject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CauceDbContext>();

        Guid? actorId = null;
        if (!string.IsNullOrWhiteSpace(keycloakSubject))
        {
            actorId = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.KeycloakId == keycloakSubject)
                .Select(user => (Guid?)user.Id)
                .FirstOrDefaultAsync(context.RequestAborted)
                .ConfigureAwait(false);
        }

        await WriteAuditAsync(dbContext, actorId, AuditActionType.Logout, context).ConfigureAwait(false);
    }

    private static async Task WriteAuditAsync(
        CauceDbContext dbContext,
        Guid? actorId,
        AuditActionType actionType,
        HttpContext context)
    {
        var auditLog = AuditLog.Record(
            actorId,
            actionType,
            nameof(Cauce.Domain.Identity.User),
            entityId: null,
            oldValuesHash: null,
            newValuesHash: null,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null,
            additionalContext: null,
            occurredAtUtc: DateTime.UtcNow);

        await dbContext.AuditLogs.AddAsync(auditLog, context.RequestAborted).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task<string?> TryReadEmailAsync(HttpContext context)
    {
        try
        {
            context.Request.Body.Position = 0;
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted).ConfigureAwait(false);
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
        }

        return null;
    }
}

/// <summary>
/// Métodos de extensión para registrar <see cref="AuditingMiddleware"/> en el pipeline.
/// </summary>
public static class AuditingMiddlewareExtensions
{
    /// <summary>
    /// Añade el middleware de auditoría de autenticación al pipeline de peticiones.
    /// </summary>
    /// <param name="app">Constructor del pipeline de la aplicación.</param>
    /// <returns>El mismo constructor para encadenamiento.</returns>
    public static IApplicationBuilder UseAuditingMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditingMiddleware>();
    }
}
