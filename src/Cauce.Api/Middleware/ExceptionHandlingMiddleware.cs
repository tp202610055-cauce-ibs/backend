using System.Diagnostics;
using Cauce.Application.Common.Exceptions;
using Cauce.Domain.Common.Exceptions;
using Cauce.Domain.Identity.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Middleware;

/// <summary>
/// Middleware que captura las excepciones no controladas del pipeline, las
/// registra sin exponer datos personales (PII) y devuelve una respuesta de error
/// estandarizada según RFC 7807 (<c>application/problem+json</c>) con un código de
/// error legible por máquina en la extensión <c>errorCode</c>.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private const string ProblemJsonContentType = "application/problem+json";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// Inicializa el middleware con el siguiente delegado del pipeline y el logger.
    /// </summary>
    /// <param name="next">Siguiente delegado en el pipeline de peticiones.</param>
    /// <param name="logger">Logger de la categoría del middleware.</param>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el middleware: invoca el resto del pipeline y traduce cualquier
    /// excepción a una respuesta de problema.
    /// </summary>
    /// <param name="context">Contexto HTTP de la petición.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception).ConfigureAwait(false);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var problemDetails = exception is ValidationException validationException
            ? BuildValidationProblem(validationException, traceId)
            : BuildProblemForException(exception, traceId);

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception of type {ExceptionType}. TraceId: {TraceId}",
                exception.GetType().Name,
                traceId);
        }
        else
        {
            _logger.LogWarning(
                "Handled exception of type {ExceptionType} mapped to {StatusCode}. TraceId: {TraceId}",
                exception.GetType().Name,
                problemDetails.Status,
                traceId);
        }

        context.Response.Clear();
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = ProblemJsonContentType;
        await context.Response.WriteAsJsonAsync(problemDetails, problemDetails.GetType()).ConfigureAwait(false);
    }

    private static ProblemDetails BuildProblemForException(Exception exception, string traceId)
    {
        var (status, title, errorCode, detail) = exception switch
        {
            DuplicateEmailException => (
                StatusCodes.Status409Conflict, "Correo duplicado", "duplicate_email", exception.Message),
            InvalidInvitationCodeException => (
                StatusCodes.Status400BadRequest, "Código de invitación inválido", "invalid_invitation_code", exception.Message),
            ExpiredInvitationCodeException => (
                StatusCodes.Status400BadRequest, "Código de invitación expirado", "expired_invitation_code", exception.Message),
            InvitationCodeAlreadyUsedException => (
                StatusCodes.Status400BadRequest, "Código de invitación ya usado", "invitation_code_already_used", exception.Message),
            AccountLockedException => (
                StatusCodes.Status423Locked, "Cuenta bloqueada", "account_locked", exception.Message),
            InvalidPasswordResetTokenException => (
                StatusCodes.Status400BadRequest, "Token de restablecimiento inválido", "invalid_password_reset_token", exception.Message),
            ExpiredPasswordResetTokenException => (
                StatusCodes.Status400BadRequest, "Token de restablecimiento expirado", "expired_password_reset_token", exception.Message),
            ConsentTextMismatchException => (
                StatusCodes.Status400BadRequest, "Consentimiento no coincide", "consent_text_mismatch", exception.Message),
            DomainException => (
                StatusCodes.Status400BadRequest, "Regla de dominio violada", "domain_rule_violation", exception.Message),
            KeycloakIntegrationException => (
                StatusCodes.Status502BadGateway, "Error del proveedor de identidad", "keycloak_integration_error",
                "No se pudo completar la operación con el proveedor de identidad."),
            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden, "Acceso denegado", "forbidden",
                "No tiene permisos para realizar esta operación."),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound, "Recurso no encontrado", "not_found",
                "El recurso solicitado no existe."),
            _ => (
                StatusCodes.Status500InternalServerError, "Error interno del servidor", "internal_server_error",
                "Ocurrió un error inesperado al procesar la solicitud.")
        };

        return BuildProblem(status, title, detail, errorCode, traceId);
    }

    private static ProblemDetails BuildValidationProblem(ValidationException exception, string traceId)
    {
        var errors = exception.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Error de validación",
            Detail = "Una o más reglas de validación no se cumplieron."
        };
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["errorCode"] = "validation_error";
        return problem;
    }

    private static ProblemDetails BuildProblem(int status, string title, string detail, string errorCode, string traceId)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["errorCode"] = errorCode;
        return problem;
    }
}

/// <summary>
/// Métodos de extensión para registrar <see cref="ExceptionHandlingMiddleware"/>
/// en el pipeline de la aplicación.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    /// <summary>
    /// Añade el middleware de manejo de excepciones al pipeline de peticiones.
    /// </summary>
    /// <param name="app">Constructor del pipeline de la aplicación.</param>
    /// <returns>El mismo constructor para encadenamiento.</returns>
    public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
