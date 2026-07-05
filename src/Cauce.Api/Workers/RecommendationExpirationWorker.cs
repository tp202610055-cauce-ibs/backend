using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.Workers;

/// <summary>
/// Worker de barrido que transita a <c>Expired</c> las recomendaciones vencidas que quedaron en un
/// estado expirable (las "recomendaciones zombie" del Prompt 4). Se ejecuta cada seis horas con una
/// actualización masiva; el actor de la auditoría (trigger) queda nulo por tratarse del sistema.
/// </summary>
public sealed class RecommendationExpirationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly RecommendationStatus[] ExpirableStatuses =
    [
        RecommendationStatus.Generated,
        RecommendationStatus.PendingReview,
        RecommendationStatus.Approved
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecommendationExpirationWorker> _logger;

    /// <summary>
    /// Inicializa el worker con sus dependencias.
    /// </summary>
    /// <param name="scopeFactory">Fábrica de ámbitos de servicio.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="logger">Logger de la categoría del worker.</param>
    public RecommendationExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RecommendationExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Workers:RecommendationExpiration:Enabled", true))
        {
            _logger.LogInformation("RecommendationExpirationWorker is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await ExpireAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while expiring recommendations.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task ExpireAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CauceDbContext>();
        var now = DateTime.UtcNow;

        var affected = await context.Recommendations
            .Where(recommendation => ExpirableStatuses.Contains(recommendation.Status)
                && recommendation.ExpiresAt != null
                && recommendation.ExpiresAt < now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(recommendation => recommendation.Status, RecommendationStatus.Expired),
                ct)
            .ConfigureAwait(false);

        if (affected > 0)
        {
            _logger.LogInformation("Expired {Count} stale recommendation(s).", affected);
        }
    }
}
