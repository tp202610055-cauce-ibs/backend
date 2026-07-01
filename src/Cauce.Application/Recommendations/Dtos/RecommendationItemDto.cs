using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.Dtos;

/// <summary>
/// Representación de un ítem de recomendación, con los nombres legibles de su alimento y, si
/// aplica, de su sustituto.
/// </summary>
/// <param name="RecommendationItemId">Identificador del ítem.</param>
/// <param name="FoodId">Identificador del alimento.</param>
/// <param name="FoodName">Nombre del alimento.</param>
/// <param name="Category">Categoría del alimento.</param>
/// <param name="ActionType">Acción dietética sugerida.</param>
/// <param name="SubstituteFoodId">Identificador del alimento sustituto, o <see langword="null"/>.</param>
/// <param name="SubstituteFoodName">Nombre del alimento sustituto, o <see langword="null"/>.</param>
/// <param name="Reasoning">Razonamiento de la acción, o <see langword="null"/>.</param>
public sealed record RecommendationItemDto(
    Guid RecommendationItemId,
    Guid FoodId,
    string FoodName,
    string Category,
    ActionType ActionType,
    Guid? SubstituteFoodId,
    string? SubstituteFoodName,
    string? Reasoning);
