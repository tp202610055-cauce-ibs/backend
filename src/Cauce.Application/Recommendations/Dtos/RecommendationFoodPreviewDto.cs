using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.Dtos;

/// <summary>
/// Alimento de una recomendación en la vista previa de una lista.
/// </summary>
/// <param name="FoodName">Nombre del alimento.</param>
/// <param name="ActionType">Acción dietética sobre el alimento.</param>
/// <param name="SubstituteFoodName">Nombre del sustituto en una sustitución, o <see langword="null"/>.</param>
public sealed record RecommendationFoodPreviewDto(string FoodName, ActionType ActionType, string? SubstituteFoodName);
