using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Recommendations.Support;

/// <summary>
/// Juego de recomendaciones con un caso por cada combinación que distinguen el archivado y la expiración. Cada
/// recomendación se identifica por una etiqueta estable, para comparar dos pasadas sobre los mismos datos.
/// </summary>
public static class RecommendationSweepDataset
{
    /// <summary>
    /// Estado de una recomendación que puede cambiar el archivado o la expiración.
    /// </summary>
    public sealed record SweepState(RecommendationStatus Status, bool IsActive, ArchiveReason? ArchiveReason);

    /// <summary>
    /// Siembra el juego para el paciente y devuelve el identificador de cada etiqueta.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, Guid>> SeedAsync(CauceDbContext db, Guid patientId, Guid nutritionistId, DateTime now)
    {
        var modelVersionId = await db.ModelVersions.Where(version => version.IsActive).Select(version => version.Id).FirstAsync();
        var foodId = await db.FoodItems.AsNoTracking().Select(food => food.Id).FirstAsync();

        Recommendation Engine(DateTime expiresAt)
        {
            var generatedAt = now.AddDays(-5);
            return Recommendation.Generate(
                patientId,
                modelVersionId,
                ConfidenceScore.Create(0.80m),
                ExplanationSource.LlmGenerated,
                "Explicación de prueba.",
                [RecommendationItem.Create(foodId, ActionType.Reduce, "razón de prueba", null)],
                generatedAt,
                expiresAt - generatedAt);
        }

        Recommendation Manual(DateTime? validUntil) =>
            Recommendation.CreateManual(patientId, nutritionistId, "Manual", "Descripción", null, "Nota clínica de prueba.", validUntil, now.AddDays(-5));

        var past = now.AddDays(-1);
        var future = now.AddDays(1);

        var generatedPast = Engine(past);

        var pendingPast = Engine(past);
        pendingPast.MarkPendingReview(now.AddDays(-5));

        var pendingFuture = Engine(future);
        pendingFuture.MarkPendingReview(now.AddDays(-5));

        var approvedPast = Engine(past);
        approvedPast.MarkPendingReview(now.AddDays(-5));
        approvedPast.Approve(nutritionistId, "Nota de aprobación.", now.AddDays(-4));

        var deliveredPast = Engine(past);
        deliveredPast.MarkPendingReview(now.AddDays(-5));
        deliveredPast.Approve(nutritionistId, "Nota de aprobación.", now.AddDays(-4));
        deliveredPast.Deliver(now.AddDays(-4));

        var rejectedPast = Engine(past);
        rejectedPast.MarkPendingReview(now.AddDays(-5));
        rejectedPast.Reject(nutritionistId, "Motivo de rechazo.", now.AddDays(-4));

        var manualArchivedPast = Manual(past);
        manualArchivedPast.Archive(ArchiveReason.PlanChange, now.AddDays(-2));

        var dataset = new Dictionary<string, Recommendation>
        {
            ["generated-past"] = generatedPast,
            ["pending-past"] = pendingPast,
            ["pending-future"] = pendingFuture,
            ["approved-past"] = approvedPast,
            ["delivered-past"] = deliveredPast,
            ["rejected-past"] = rejectedPast,
            ["manual-past"] = Manual(past),
            ["manual-future"] = Manual(future),
            ["manual-without-validity"] = Manual(null),
            ["manual-archived-past"] = manualArchivedPast
        };

        db.Recommendations.AddRange(dataset.Values);
        await db.SaveChangesAsync();
        return dataset.ToDictionary(pair => pair.Key, pair => pair.Value.Id);
    }

    /// <summary>
    /// Lee el estado actual de cada etiqueta.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, SweepState>> SnapshotAsync(CauceDbContext db, IReadOnlyDictionary<string, Guid> ids)
    {
        var values = ids.Values.ToList();
        var states = await db.Recommendations.AsNoTracking()
            .Where(recommendation => values.Contains(recommendation.Id))
            .ToDictionaryAsync(recommendation => recommendation.Id, recommendation => new SweepState(recommendation.Status, recommendation.IsActive, recommendation.ArchiveReason));
        return ids.ToDictionary(pair => pair.Key, pair => states[pair.Value]);
    }
}
