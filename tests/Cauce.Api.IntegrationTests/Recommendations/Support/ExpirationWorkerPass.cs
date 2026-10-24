using Cauce.Api.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cauce.Api.IntegrationTests.Recommendations.Support;

/// <summary>
/// Ejecuta una pasada real de <see cref="RecommendationExpirationWorker"/>: el worker barre al arrancar y se detiene
/// en cuanto informa cuántas recomendaciones expiró. El juego de datos debe tener al menos una vencida.
/// </summary>
public static class ExpirationWorkerPass
{
    /// <summary>
    /// Arranca el worker con los servicios de la aplicación y espera su primera pasada.
    /// </summary>
    public static async Task RunAsync(IServiceProvider services)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Workers:RecommendationExpiration:Enabled"] = "true" })
            .Build();
        var logger = new PassLogger("Expired");
        var worker = new RecommendationExpirationWorker(services.GetRequiredService<IServiceScopeFactory>(), configuration, logger);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await logger.Pass.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private sealed class PassLogger(string marker) : ILogger<RecommendationExpirationWorker>
    {
        private readonly TaskCompletionSource _pass = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Pass => _pass.Task;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null)
            {
                _pass.TrySetException(exception);
            }
            else if (formatter(state, exception).StartsWith(marker, StringComparison.Ordinal))
            {
                _pass.TrySetResult();
            }
        }
    }
}
