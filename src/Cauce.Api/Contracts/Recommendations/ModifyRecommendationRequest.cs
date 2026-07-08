using Cauce.Application.Recommendations.UseCases.ModifyRecommendation;

namespace Cauce.Api.Contracts.Recommendations;

/// <summary>
/// Cuerpo de la petición de aprobación con modificación de una recomendación (US17 CA03).
/// </summary>
/// <param name="ClinicalNote">Nota clínica de la modificación.</param>
/// <param name="Items">Nuevos ítems que reemplazan a los actuales, o <see langword="null"/> para conservarlos.</param>
/// <param name="Title">Nuevo título, o <see langword="null"/> para conservarlo.</param>
/// <param name="Description">Nueva descripción, o <see langword="null"/> para conservarla.</param>
/// <param name="Steps">Nuevos pasos, o <see langword="null"/> para conservarlos.</param>
public sealed record ModifyRecommendationRequest(
    string ClinicalNote,
    IReadOnlyList<ModifyRecommendationItemInput>? Items,
    string? Title,
    string? Description,
    IReadOnlyList<string>? Steps);
