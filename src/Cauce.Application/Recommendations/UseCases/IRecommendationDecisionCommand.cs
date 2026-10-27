namespace Cauce.Application.Recommendations.UseCases;

/// <summary>
/// Comando de un nutricionista que decide sobre una recomendación existente. Si el servidor lo rechaza
/// porque la recomendación ya está cerrada, el intento queda auditado.
/// </summary>
public interface IRecommendationDecisionCommand
{
    /// <summary>Recomendación sobre la que se decide.</summary>
    Guid RecommendationId { get; }

    /// <summary>Acción intentada, en minúsculas: <c>approve</c>, <c>modify</c>, <c>reject</c> o <c>archive</c>.</summary>
    string AttemptedAction { get; }
}
