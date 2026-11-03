using System.Text.Json;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder idempotente que registra la versión del motor configurado y la deja como la única activa. Corre con
/// seed-catalogs en cualquier ambiente; el nombre y el hash salen del descriptor del motor.
/// </summary>
public sealed class RecommendationsModelVersionsSeeder
{
    private const int RuleDatasetRows = 250_000;
    private const int XgboostTrainingRows = 175_000;

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
    /// Registra la versión del motor si falta y la activa, desactivando antes la versión anterior.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    /// <exception cref="InvalidOperationException">Si la versión registrada tiene otro hash que el motor.</exception>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var descriptor = _engine.Descriptor;

        var target = await _context.Set<ModelVersion>()
            .FirstOrDefaultAsync(version => version.VersionName == descriptor.VersionName, ct)
            .ConfigureAwait(false);
        if (target is { IsActive: true })
        {
            _logger.LogInformation("Model version '{Version}' already seeded.", descriptor.VersionName);
            return;
        }

        if (target is not null && !target.VerifyIntegrity(descriptor.ModelHash))
        {
            throw new InvalidOperationException(
                $"La versión '{descriptor.VersionName}' ya está registrada con otro SHA-256 que el motor.");
        }

        // Dos guardados: el índice de versión activa única exige desactivar la anterior antes de activar.
        await using var transaction = await _context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        var active = await _context.Set<ModelVersion>()
            .FirstOrDefaultAsync(version => version.IsActive, ct)
            .ConfigureAwait(false);
        if (active is not null)
        {
            active.Deactivate();
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        target ??= await RegisterAsync(descriptor, ct).ConfigureAwait(false);
        target.Activate();

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Seeded model version '{Version}' as active.", descriptor.VersionName);
    }

    private async Task<ModelVersion> RegisterAsync(ModelVersionDescriptor descriptor, CancellationToken ct)
    {
        var modelVersion = ModelVersion.Register(
            descriptor.VersionName,
            descriptor.ModelHash,
            trainingDatasetSize: ResolveDatasetSize(descriptor),
            performanceMetricsJson: BuildMetricsJson(descriptor),
            deployedBy: "system-seeder",
            deployedAt: DateTime.UtcNow,
            isDummy: descriptor.IsDummy);

        await _context.Set<ModelVersion>().AddAsync(modelVersion, ct).ConfigureAwait(false);
        return modelVersion;
    }

    private static int? ResolveDatasetSize(ModelVersionDescriptor descriptor)
    {
        if (descriptor.IsDummy)
        {
            return null;
        }

        return descriptor.EngineKind == RecommendationsOptions.XgboostEngineKind
            ? XgboostTrainingRows
            : RuleDatasetRows;
    }

    private static string BuildMetricsJson(ModelVersionDescriptor descriptor)
    {
        if (descriptor.IsDummy)
        {
            return JsonSerializer.Serialize(new
            {
                engine_kind = descriptor.EngineKind,
                disclaimer = "Fictitious 5-feature model for technical demonstration only: " +
                    "no metrics, no clinical validity."
            });
        }

        return descriptor.EngineKind == RecommendationsOptions.XgboostEngineKind
            ? BuildXgboostMetricsJson(descriptor)
            : BuildRuleMetricsJson(descriptor);
    }

    private static string BuildRuleMetricsJson(ModelVersionDescriptor descriptor)
    {
        return JsonSerializer.Serialize(new
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
    }

    private static string BuildXgboostMetricsJson(ModelVersionDescriptor descriptor)
    {
        return JsonSerializer.Serialize(new
        {
            engine_kind = descriptor.EngineKind,
            source_dataset_reference = "fodmap_synthetic_dataset_1.xlsx",
            source_dataset_sha256 = "e3a48b5c233423a084509afaf8545a61a878898d084b0b372fba6e1a20ca8e06",
            feature_count = 10,
            test_metrics = new
            {
                accuracy = 0.7215,
                precision = 0.8112,
                recall = 0.5902,
                f1 = 0.6833,
                auc_roc = 0.7674
            },
            disclaimer = "Trained on synthetic data: proof of concept without clinical validity."
        });
    }
}
