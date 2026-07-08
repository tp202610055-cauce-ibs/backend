using System.Net;
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
