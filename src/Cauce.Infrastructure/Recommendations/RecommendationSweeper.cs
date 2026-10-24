using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Recommendations;

/// <summary>
/// Barridos masivos de recomendaciones que comparten los workers y los comandos a demanda: el archivado por
/// vigencia vencida y la expiración de las que quedaron en un estado expirable. El actor de la auditoría queda nulo.
/// </summary>
public sealed class RecommendationSweeper
{
    private static readonly RecommendationStatus[] ApprovedTerminalStatuses =
    [
        RecommendationStatus.Approved,
        RecommendationStatus.ModifiedApproved,
        RecommendationStatus.ManualApproved
    ];

    private static readonly RecommendationStatus[] ExpirableStatuses =
    [
        RecommendationStatus.Generated,
        RecommendationStatus.PendingReview,
        RecommendationStatus.Approved
    ];

    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el barrido sobre el contexto del ámbito.
    /// </summary>
    public RecommendationSweeper(CauceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Archiva las recomendaciones activas en estado terminal aprobado cuya vigencia (<c>ValidUntil</c>) venció, con
    /// <see cref="ArchiveReason.TemporalExpiration"/>. Devuelve cuántas archivó.
    /// </summary>
    public Task<int> ArchiveDueAsync(DateTime nowUtc, CancellationToken ct)
    {
        return _context.Recommendations
            .Where(recommendation => recommendation.IsActive
                && recommendation.ValidUntil != null
                && recommendation.ValidUntil < nowUtc
                && ApprovedTerminalStatuses.Contains(recommendation.Status))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(recommendation => recommendation.IsActive, false)
                    .SetProperty(recommendation => recommendation.ArchiveReason, ArchiveReason.TemporalExpiration)
                    .SetProperty(recommendation => recommendation.ArchivedAt, nowUtc),
                ct);
    }

    /// <summary>
    /// Pasa a <c>Expired</c> las recomendaciones en estado expirable cuya ventana (<c>ExpiresAt</c>) venció. Devuelve
    /// cuántas expiró.
    /// </summary>
    public Task<int> ExpireDueAsync(DateTime nowUtc, CancellationToken ct)
    {
        return _context.Recommendations
            .Where(recommendation => ExpirableStatuses.Contains(recommendation.Status)
                && recommendation.ExpiresAt != null
                && recommendation.ExpiresAt < nowUtc)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(recommendation => recommendation.Status, RecommendationStatus.Expired),
                ct);
    }
}
