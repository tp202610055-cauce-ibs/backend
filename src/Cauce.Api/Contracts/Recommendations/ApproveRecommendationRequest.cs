namespace Cauce.Api.Contracts.Recommendations;

/// <summary>
/// Cuerpo de la petición para aprobar una recomendación.
/// </summary>
/// <param name="Note">Nota clínica de aprobación (entre 10 y 2000 caracteres).</param>
public sealed record ApproveRecommendationRequest(string Note);
