using System.Text.Json;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Domain.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder idempotente que inserta, solo en desarrollo, la versión de modelo de la regla activa
/// (DEC-B4-10). El nombre y el hash se derivan del descriptor del motor registrado, de modo que un
/// cambio en la fórmula produce una versión nueva. En producción, la inserción se hace vía script
/// administrativo out-of-band.
/// </summary>
public sealed class RecommendationsModelVersionsSeeder
{
    private readonly CauceDbContext _context;
    private readonly IRecommendationEngine _engine;
    private readonly ILogger<RecommendationsModelVersionsSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="engine">Motor de recomendaciones activo.</param>
    /// <param name="logger">Logger de la categoría del seeder.</param>
    public RecommendationsModelVersionsSeeder(
        CauceDbContext context,
        IRecommendationEngine engine,
        ILogger<RecommendationsModelVersionsSeeder> logger)
    {
        _context = context;
        _engine = engine;
        _logger = logger;
    }

    /// <summary>
    /// Siembra la versión de modelo de la regla de forma idempotente.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var descriptor = _engine.Descriptor;

        var exists = await _context.Set<ModelVersion>()
            .AnyAsync(version => version.VersionName == descriptor.VersionName, ct)
            .ConfigureAwait(false);
        if (exists)
        {
            _logger.LogInformation("Model version '{Version}' already seeded.", descriptor.VersionName);
            return;
        }

        var metricsJson = JsonSerializer.Serialize(new
        {
            engine_kind = descriptor.EngineKind,
            source_dataset_reference = "fodmap_synthetic_dataset 2026-06-29",
            linear_regression_r2_vs_riesgo_fodmap = 0.9413,
            replicated_metrics = new
            {
                accuracy = 0.7212,
                precision = 0.8112,
                recall = 0.5895,
                f1 = 0.6828,
                auc_roc = 0.7671
            },
            disclaimer = "Replicated from XGBoost notebook for traceability. " +
                "Current production engine is rule-based, not the XGBoost model."
        });

        var modelVersion = ModelVersion.Register(
            descriptor.VersionName,
            descriptor.ModelHash,
            trainingDatasetSize: 250_000,
            performanceMetricsJson: metricsJson,
            deployedBy: "system-seeder",
            deployedAt: DateTime.UtcNow);

        modelVersion.Activate();

        await _context.Set<ModelVersion>().AddAsync(modelVersion, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Seeded model version '{Version}' as active.", descriptor.VersionName);
    }
}
