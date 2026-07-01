using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;

namespace Cauce.Domain.Recommendations.Services;

/// <summary>
/// Máquina de estados de la recomendación: define las transiciones permitidas entre los
/// siete estados del flujo Human-In-The-Loop y centraliza su validación.
/// </summary>
public static class RecommendationStateMachine
{
    private static readonly IReadOnlyDictionary<RecommendationStatus, RecommendationStatus[]> Transitions =
        new Dictionary<RecommendationStatus, RecommendationStatus[]>
        {
            [RecommendationStatus.Generated] = new[]
            {
                RecommendationStatus.PendingReview,
                RecommendationStatus.Approved,
                RecommendationStatus.Expired
            },
            [RecommendationStatus.PendingReview] = new[]
            {
                RecommendationStatus.Approved,
                RecommendationStatus.Rejected,
                RecommendationStatus.Expired
            },
            [RecommendationStatus.Approved] = new[]
            {
                RecommendationStatus.Delivered,
                RecommendationStatus.Expired
            },
            [RecommendationStatus.Delivered] = new[]
            {
                RecommendationStatus.FeedbackReceived
            },
            [RecommendationStatus.Rejected] = Array.Empty<RecommendationStatus>(),
            [RecommendationStatus.FeedbackReceived] = Array.Empty<RecommendationStatus>(),
            [RecommendationStatus.Expired] = Array.Empty<RecommendationStatus>()
        };

    /// <summary>
    /// Indica si la transición del estado de origen al de destino está permitida.
    /// </summary>
    /// <param name="from">Estado de origen.</param>
    /// <param name="to">Estado de destino.</param>
    /// <returns><see langword="true"/> si la transición es válida.</returns>
    public static bool CanTransition(RecommendationStatus from, RecommendationStatus to)
    {
        return Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    /// <summary>
    /// Verifica que la transición sea válida; en caso contrario lanza una excepción.
    /// </summary>
    /// <param name="from">Estado de origen.</param>
    /// <param name="to">Estado de destino.</param>
    /// <exception cref="InvalidRecommendationStateTransitionException">Si la transición no está permitida.</exception>
    public static void EnsureTransition(RecommendationStatus from, RecommendationStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidRecommendationStateTransitionException(from, to);
        }
    }
}
