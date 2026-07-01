using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Alimento candidato para una recomendación: un alimento del catálogo que el paciente
/// consumió dentro de la ventana evaluada, con sus niveles FODMAP granulares y la cantidad
/// promedio consumida.
/// </summary>
/// <param name="FoodId">Identificador del alimento del catálogo.</param>
/// <param name="Name">Nombre del alimento.</param>
/// <param name="Category">Categoría del alimento.</param>
/// <param name="FodmapLevel">Nivel de carga FODMAP global.</param>
/// <param name="OligosLevel">Nivel ordinal de oligosacáridos (0–2).</param>
/// <param name="FructoseLevel">Nivel ordinal de fructosa (0–2).</param>
/// <param name="PolyolsLevel">Nivel ordinal de polioles (0–2).</param>
/// <param name="LactoseLevel">Nivel ordinal de lactosa (0–2).</param>
/// <param name="QuantityGrams">Cantidad promedio consumida, aproximada en gramos.</param>
public sealed record CandidateFood(
    Guid FoodId,
    string Name,
    string Category,
    FodmapLevel FodmapLevel,
    byte OligosLevel,
    byte FructoseLevel,
    byte PolyolsLevel,
    byte LactoseLevel,
    int QuantityGrams);
