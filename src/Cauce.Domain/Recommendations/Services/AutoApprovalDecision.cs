namespace Cauce.Domain.Recommendations.Services;

/// <summary>
/// Resultado de la evaluación del guard de auto-aprobación: si la recomendación puede
/// auto-aprobarse y, en caso negativo, las razones que lo impidieron.
/// </summary>
/// <param name="ShouldAutoApprove">Indica si la recomendación puede auto-aprobarse.</param>
/// <param name="Reasons">Razones por las que no se auto-aprobó; vacío si se auto-aprueba.</param>
public sealed record AutoApprovalDecision(bool ShouldAutoApprove, IReadOnlyList<string> Reasons);
