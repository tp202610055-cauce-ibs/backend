using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Resumen de una entrada del catálogo de alimentos para listados y búsquedas.
/// </summary>
/// <param name="FoodId">Identificador del alimento.</param>
/// <param name="Name">Nombre del alimento.</param>
/// <param name="Category">Categoría del alimento.</param>
/// <param name="FodmapLevel">Nivel de carga FODMAP.</param>
/// <param name="IsPeruvian">Indica si el alimento es peruano.</param>
public sealed record FoodItemSummary(
    Guid FoodId,
    string Name,
    string Category,
    FodmapLevel FodmapLevel,
    bool IsPeruvian);
