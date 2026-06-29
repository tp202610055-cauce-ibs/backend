using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Infrastructure.ClinicalRegistry;

/// <summary>
/// Implementación de <see cref="IFodmapAggregator"/>. Usa una heurística conservadora:
/// el nivel FODMAP de la comida es el máximo nivel entre sus ítems con peso positivo, ya
/// que clínicamente la presencia de un ingrediente de carga alta puede desencadenar
/// síntomas con independencia de su proporción.
/// </summary>
public sealed class FodmapAggregator : IFodmapAggregator
{
    /// <inheritdoc />
    public FodmapLevel AggregateForMeal(IEnumerable<(FodmapLevel itemLevel, decimal weightGrams)> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        // TODO (clinical): refinar con umbrales de carga acumulada por porción según Monash
        // University cuando Mirian provea los datos. Por ahora, regla de máximo nivel.
        var aggregated = FodmapLevel.Low;
        foreach (var (itemLevel, weightGrams) in items)
        {
            if (weightGrams > 0m && itemLevel > aggregated)
            {
                aggregated = itemLevel;
            }
        }

        return aggregated;
    }
}
