using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Recommendations;

/// <summary>
/// Con el motor Xgboost, lo crea al arrancar y exige que la versión activa en la base sea la suya, porque
/// cada recomendación se vincula a la versión activa. Con los demás motores no hace nada.
/// </summary>
public sealed class RecommendationEngineStartupVerifier : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RecommendationsOptions _options;
    private readonly ILogger<RecommendationEngineStartupVerifier> _logger;

    /// <summary>
    /// Inicializa el verificador con sus dependencias.
    /// </summary>
    /// <param name="serviceProvider">Proveedor raíz; el motor y el repositorio se resuelven solo con Xgboost.</param>
    /// <param name="options">Opciones del módulo de recomendaciones.</param>
    /// <param name="logger">Logger de la categoría del verificador.</param>
    public RecommendationEngineStartupVerifier(
        IServiceProvider serviceProvider,
        IOptions<RecommendationsOptions> options,
        ILogger<RecommendationEngineStartupVerifier> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.EngineKind != RecommendationsOptions.XgboostEngineKind)
        {
            return;
        }

        var descriptor = _serviceProvider.GetRequiredService<IRecommendationEngine>().Descriptor;

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IModelVersionRepository>();
        var active = await repository.GetActiveAsync(cancellationToken).ConfigureAwait(false);

        if (active is null
            || active.VersionName != descriptor.VersionName
            || !active.VerifyIntegrity(descriptor.ModelHash))
        {
            throw new InvalidOperationException(
                $"La versión activa de model_versions ('{active?.VersionName ?? "ninguna"}') no coincide en nombre " +
                $"y SHA-256 con el motor configurado ('{descriptor.VersionName}'). Ejecuta seed-catalogs con " +
                $"Recommendations:EngineKind = '{RecommendationsOptions.XgboostEngineKind}'.");
        }

        _logger.LogInformation(
            "Active model version matches the configured engine: {Version}.",
            descriptor.VersionName);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
