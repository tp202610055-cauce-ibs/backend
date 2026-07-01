namespace Cauce.Infrastructure.Recommendations.Readers;

/// <summary>
/// Entrada mínima del catálogo de alimentos usada por el <see cref="AllergyHeuristicMatcher"/>
/// para decidir si un alimento debe excluirse por una alergia.
/// </summary>
/// <param name="Id">Identificador del alimento.</param>
/// <param name="Name">Nombre del alimento.</param>
/// <param name="Category">Categoría del alimento.</param>
/// <param name="FodmapTags">Etiquetas FODMAP del alimento, o <see langword="null"/>.</param>
public sealed record FoodCatalogEntry(Guid Id, string Name, string Category, string? FodmapTags);
