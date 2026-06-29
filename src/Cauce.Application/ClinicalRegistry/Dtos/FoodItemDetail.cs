using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Detalle completo de una entrada del catálogo de alimentos, con sus valores
/// nutricionales por 100 g.
/// </summary>
/// <param name="FoodId">Identificador del alimento.</param>
/// <param name="Name">Nombre del alimento.</param>
/// <param name="Category">Categoría del alimento.</param>
/// <param name="CaloriesPer100g">Energía por 100 g.</param>
/// <param name="ProteinGPer100g">Proteína por 100 g.</param>
/// <param name="CarbsGPer100g">Carbohidratos por 100 g.</param>
/// <param name="FatGPer100g">Grasa por 100 g.</param>
/// <param name="FiberGPer100g">Fibra por 100 g.</param>
/// <param name="FodmapLevel">Nivel de carga FODMAP.</param>
/// <param name="FodmapTags">Etiquetas FODMAP, o <see langword="null"/>.</param>
/// <param name="IsPeruvian">Indica si el alimento es peruano.</param>
/// <param name="IsActive">Indica si el alimento está activo.</param>
public sealed record FoodItemDetail(
    Guid FoodId,
    string Name,
    string Category,
    decimal CaloriesPer100g,
    decimal ProteinGPer100g,
    decimal CarbsGPer100g,
    decimal FatGPer100g,
    decimal FiberGPer100g,
    FodmapLevel FodmapLevel,
    string? FodmapTags,
    bool IsPeruvian,
    bool IsActive);
