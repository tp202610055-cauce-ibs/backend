using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Utilidades compartidas para construir recomendaciones de prueba en estados específicos.
/// </summary>
internal static class RecommendationTestData
{
    public static Recommendation Generated(Guid patientId, DateTime now, TimeSpan? window = null)
    {
        var items = new[] { RecommendationItem.Create(Guid.NewGuid(), ActionType.Avoid, "razón", null) };
        return Recommendation.Generate(
            patientId,
            Guid.NewGuid(),
            ConfidenceScore.Create(0.5m),
            ExplanationSource.LlmGenerated,
            "explicación de prueba",
            items,
            now,
            window ?? TimeSpan.FromHours(72));
    }

    public static Recommendation PendingReview(Guid patientId, DateTime now)
    {
        var recommendation = Generated(patientId, now);
        recommendation.MarkPendingReview(now);
        return recommendation;
    }

    public static Recommendation Approved(Guid patientId, DateTime now)
    {
        var recommendation = PendingReview(patientId, now);
        recommendation.Approve(Guid.NewGuid(), "nota clínica válida", now);
        return recommendation;
    }

    public static Recommendation Delivered(Guid patientId, DateTime now)
    {
        var recommendation = Approved(patientId, now);
        recommendation.Deliver(now);
        return recommendation;
    }
}
