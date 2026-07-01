using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Ítem que alimenta al orquestador de explicaciones: el nombre legible del alimento, su
/// acción y su razonamiento. Lo construye el caso de uso a partir de los ítems de la
/// recomendación y los nombres de los alimentos candidatos, ya que la entidad
/// <c>RecommendationItem</c> solo conserva identificadores.
/// </summary>
/// <param name="FoodName">Nombre legible del alimento.</param>
/// <param name="ActionType">Acción dietética sugerida.</param>
/// <param name="Reasoning">Razonamiento de la acción, o <see langword="null"/>.</param>
public sealed record ExplanationItem(
    string FoodName,
    ActionType ActionType,
    string? Reasoning);
