using System.Diagnostics;
using Cauce.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;

namespace Cauce.Api.Middleware;

/// <summary>
/// Middleware que captura las excepciones no controladas del pipeline, las
/// registra sin exponer datos personales (PII) y devuelve una respuesta de error
/// estandarizada según RFC 7807 (<c>application/problem+json</c>).
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

        var problemDetails = exception switch
        {
            ValidationException validationException => BuildValidationProblem(validationException, traceId),
            DomainException domainException => BuildDomainProblem(domainException, traceId),
            UnauthorizedAccessException => BuildProblem(
                StatusCodes.Status401Unauthorized,
                "No autorizado",
                "No tiene permisos para realizar esta operación.",
                traceId),
            KeyNotFoundException => BuildProblem(
                StatusCodes.Status404NotFound,
                "Recurso no encontrado",
                "El recurso solicitado no existe.",
                traceId),
            _ => BuildProblem(
                StatusCodes.Status500InternalServerError,
                "Error interno del servidor",
                "Ocurrió un error inesperado al procesar la solicitud.",
                traceId)
        };

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
        return problem;
    }

    private static ProblemDetails BuildDomainProblem(DomainException exception, string traceId)
    {
        var status = exception.GetType().Name.Contains("AlreadyUsed", StringComparison.OrdinalIgnoreCase)
            || exception.GetType().Name.Contains("Conflict", StringComparison.OrdinalIgnoreCase)
            || exception.GetType().Name.Contains("AlreadyRegistered", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;

        return BuildProblem(status, "Regla de dominio violada", exception.Message, traceId);
    }

    private static ProblemDetails BuildProblem(int status, string title, string detail, string traceId)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
        problem.Extensions["traceId"] = traceId;
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
