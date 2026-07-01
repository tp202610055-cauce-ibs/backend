using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;

namespace Cauce.Domain.Recommendations.Services;

/// <summary>
/// Guard de dominio que decide si una recomendación puede auto-aprobarse sin revisión
/// humana, según el feature flag de auto-aprobación y los criterios clínicos (DEC-B4-04 y
/// DEC-B4-05). Durante el piloto el flag está deshabilitado y el guard nunca auto-aprueba.
/// </summary>
public sealed class AutoApprovalGuard
{
    /// <summary>
    /// Evalúa si la recomendación puede auto-aprobarse.
    /// </summary>
    /// <param name="autoApprovalEnabled">Indica si la auto-aprobación está habilitada por configuración.</param>
    /// <param name="threshold">Umbral mínimo de confianza para auto-aprobar.</param>
    /// <param name="maxAvoidItems">Cantidad máxima de ítems de tipo <see cref="ActionType.Avoid"/> tolerada.</param>
    /// <param name="confidence">Puntaje de confianza de la recomendación.</param>
    /// <param name="items">Ítems de la recomendación.</param>
    /// <returns>La decisión de auto-aprobación con sus razones.</returns>
    public AutoApprovalDecision Evaluate(
        bool autoApprovalEnabled,
        decimal threshold,
        int maxAvoidItems,
        ConfidenceScore confidence,
        IReadOnlyList<RecommendationItem> items)
    {
        ArgumentNullException.ThrowIfNull(confidence);
        ArgumentNullException.ThrowIfNull(items);

        if (!autoApprovalEnabled)
        {
            return new AutoApprovalDecision(false, new[] { "AutoApprovalDisabled" });
        }

        if (confidence.Value < threshold)
        {
            return new AutoApprovalDecision(false, new[] { "ConfidenceBelowThreshold" });
        }

        var avoidCount = items.Count(item => item.ActionType == ActionType.Avoid);
        if (avoidCount > maxAvoidItems)
        {
            return new AutoApprovalDecision(false, new[] { "TooManyAvoidItems" });
        }

        return new AutoApprovalDecision(true, Array.Empty<string>());
    }
}
