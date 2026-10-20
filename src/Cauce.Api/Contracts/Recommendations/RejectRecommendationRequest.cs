namespace Cauce.Api.Contracts.Recommendations;

/// <summary>
/// Cuerpo de la petición para rechazar una recomendación.
/// </summary>
/// <param name="Reason">Motivo del rechazo (entre 10 y 2000 caracteres sin contar los espacios de los extremos).</param>
public sealed record RejectRecommendationRequest(string Reason);
