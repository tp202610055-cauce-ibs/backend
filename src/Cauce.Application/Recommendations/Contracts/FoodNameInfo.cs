namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Datos legibles de un alimento del catálogo, usados para enriquecer el detalle de una
/// recomendación con nombres y categorías a partir de identificadores.
/// </summary>
/// <param name="Name">Nombre del alimento.</param>
/// <param name="Category">Categoría del alimento.</param>
public sealed record FoodNameInfo(string Name, string Category);
