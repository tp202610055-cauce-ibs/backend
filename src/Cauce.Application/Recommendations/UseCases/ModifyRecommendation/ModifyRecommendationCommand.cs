using Cauce.Domain.Recommendations.Enums;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.ModifyRecommendation;

/// <summary>
/// Comando para aprobar una recomendación tras modificarla (US17 CA03). Solo válido sobre
/// recomendaciones en revisión pendiente.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación.</param>
/// <param name="ClinicalNote">Nota clínica de la modificación.</param>
/// <param name="Items">Nuevos ítems que reemplazan a los actuales, o <see langword="null"/> para conservarlos.</param>
/// <param name="Title">Nuevo título, o <see langword="null"/> para conservarlo.</param>
/// <param name="Description">Nueva descripción, o <see langword="null"/> para conservarla.</param>
/// <param name="Steps">Nuevos pasos, o <see langword="null"/> para conservarlos.</param>
public sealed record ModifyRecommendationCommand(
    Guid RecommendationId,
    string ClinicalNote,
    IReadOnlyList<ModifyRecommendationItemInput>? Items,
    string? Title,
    string? Description,
    IReadOnlyList<string>? Steps) : IRequest<Unit>;

/// <summary>
/// Ítem propuesto en una modificación de recomendación.
/// </summary>
/// <param name="FoodId">Identificador del alimento del catálogo.</param>
/// <param name="ActionType">Acción dietética sugerida.</param>
/// <param name="Reasoning">Razonamiento de la acción, opcional.</param>
/// <param name="SubstituteFoodId">Identificador del alimento sustituto, requerido solo en sustituciones.</param>
public sealed record ModifyRecommendationItemInput(
    Guid FoodId,
    ActionType ActionType,
    string? Reasoning,
    Guid? SubstituteFoodId);
