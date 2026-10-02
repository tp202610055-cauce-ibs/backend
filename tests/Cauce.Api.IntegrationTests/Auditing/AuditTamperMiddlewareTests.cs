using System.Net;
using System.Security.Claims;
using Cauce.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Cauce.Api.IntegrationTests.Auditing;

/// <summary>
/// Pruebas del <see cref="ExceptionHandlingMiddleware"/> ante un intento de modificar <c>audit_logs</c>
/// (TS04 CA02): detecta la <see cref="PostgresException"/> de violación de la inmutabilidad, responde
/// 403 y registra un evento de seguridad de nivel Error. No requiere Docker.
/// </summary>
public sealed class AuditTamperMiddlewareTests
{
    [Fact]
    public async Task Invoke_AuditLogTamperPostgresException_Returns403AndLogsSecurityEvent()
    {
        var pgException = new PostgresException(
            "audit_logs is immutable: UPDATE operations are not allowed on this table",
            "ERROR", "ERROR", PostgresErrorCodes.CheckViolation);
        var logger = new CapturingLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw pgException, logger);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Error
            && entry.Message.Contains("audit_tamper_attempt")
            && entry.Message.Contains("audit_logs"));
    }

    [Fact]
    public async Task Invoke_TamperAttemptByAuthenticatedUser_LogsTheSubjectIdAndNeverTheEmail()
    {
        // Como en Program.cs: el mapeo de claims renombra "sub" a NameIdentifier y Identity.Name sale de
        // preferred_username, que en el realm es el correo (acta A70).
        const string subject = "b8ebd09c-3bb3-4e7b-90dd-a55124bae0fd";
        const string email = "ana.canario@cauce.local";
        var pgException = new PostgresException(
            "audit_logs is immutable: UPDATE operations are not allowed on this table",
            "ERROR", "ERROR", PostgresErrorCodes.CheckViolation);
        var logger = new CapturingLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw pgException, logger);
        var context = NewContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, subject), new Claim("preferred_username", email)],
            authenticationType: "Bearer",
            nameType: "preferred_username",
            roleType: ClaimTypes.Role));

        await middleware.InvokeAsync(context);

        logger.Entries.Should().Contain(entry => entry.Message.Contains("audit_tamper_attempt") && entry.Message.Contains(subject));
        logger.Entries.Should().NotContain(entry => entry.Message.Contains(email), "un log nunca lleva el correo (Ley 29733)");
    }

    [Fact]
    public async Task Invoke_UnrelatedPostgresException_DoesNotLogSecurityEvent()
    {
        var pgException = new PostgresException(
            "duplicate key value violates unique constraint", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
        var logger = new CapturingLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw pgException, logger);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        logger.Entries.Should().NotContain(entry => entry.Message.Contains("audit_tamper_attempt"));
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
