using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Domain.Recommendations.Services;

/// <summary>
/// Calcula las acciones que quien consulta una recomendación puede ejecutar sobre ella en este momento.
/// </summary>
public static class RecommendationActionPolicy
{
    private static readonly IReadOnlyList<RecommendationAction> ReviewActions =
        [RecommendationAction.Approve, RecommendationAction.Modify, RecommendationAction.Reject];

    private static readonly IReadOnlyList<RecommendationAction> ArchiveActions = [RecommendationAction.Archive];

    /// <summary>
    /// Devuelve las acciones permitidas; vacía si quien consulta no es un nutricionista asignado al paciente.
    /// </summary>
    /// <param name="recommendation">Recomendación consultada.</param>
    /// <param name="now">Marca de tiempo UTC de referencia.</param>
    /// <param name="callerIsAssignedNutritionist">Indica si quien consulta es un nutricionista asignado al paciente.</param>
    public static IReadOnlyList<RecommendationAction> GetAllowedActions(
        Recommendation recommendation,
        DateTime now,
        bool callerIsAssignedNutritionist)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        if (!callerIsAssignedNutritionist)
        {
            return [];
        }

        if (recommendation.Status == RecommendationStatus.PendingReview && !recommendation.IsExpired(now))
        {
            return ReviewActions;
        }

        if (recommendation.IsActive && recommendation.IsApprovedTerminalState())
        {
            return ArchiveActions;
        }

        return [];
    }
}
