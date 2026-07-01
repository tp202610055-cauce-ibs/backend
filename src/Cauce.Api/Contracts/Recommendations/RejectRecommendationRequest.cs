namespace Cauce.Api.Contracts.Recommendations;

/// <summary>
/// Cuerpo de la petición para rechazar una recomendación.
/// </summary>
/// <param name="Reason">Motivo del rechazo (entre 10 y 2000 caracteres).</param>
public sealed record RejectRecommendationRequest(string Reason);
