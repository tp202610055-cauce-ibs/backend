using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Calcula la carga FODMAP agregada de una comida a partir de los niveles FODMAP y los
/// pesos de sus ítems.
/// </summary>
public interface IFodmapAggregator
{
    /// <summary>
    /// Agrega los niveles FODMAP de los ítems de una comida en un nivel representativo.
    /// </summary>
    /// <param name="items">Pares (nivel FODMAP del ítem, peso en gramos).</param>
    /// <returns>El nivel FODMAP agregado de la comida.</returns>
    FodmapLevel AggregateForMeal(IEnumerable<(FodmapLevel itemLevel, decimal weightGrams)> items);
}
