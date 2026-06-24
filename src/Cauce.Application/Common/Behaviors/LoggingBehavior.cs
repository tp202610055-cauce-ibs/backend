using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Common.Behaviors;

/// <summary>
/// Comportamiento del pipeline de MediatR que registra el inicio y el fin de la
/// ejecución de cada handler junto con su duración. No registra el contenido de
/// la petición ni de la respuesta, ya que pueden contener datos personales (PII).
/// </summary>
/// <typeparam name="TRequest">Tipo de la petición.</typeparam>
/// <typeparam name="TResponse">Tipo de la respuesta.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Inicializa el comportamiento con el logger.
    /// </summary>
    /// <param name="logger">Logger de la categoría del comportamiento.</param>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next().ConfigureAwait(false);
            stopwatch.Stop();
            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            _logger.LogError(
                exception,
                "Failed {RequestName} after {ElapsedMs}ms: {ExceptionType}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                exception.GetType().Name);
            throw;
        }
    }
}
