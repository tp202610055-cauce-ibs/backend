using Cauce.Application.Recommendations.Contracts;

namespace Cauce.Application.Common.Interfaces.Recommendations;

/// <summary>
/// Lector del catálogo de alimentos para enriquecer el detalle de una recomendación con
/// nombres y categorías legibles a partir de los identificadores de sus ítems.
/// </summary>
public interface IFoodCatalogReader
{
    /// <summary>
    /// Obtiene los datos legibles de los alimentos indicados, indexados por identificador.
    /// </summary>
    /// <param name="foodIds">Identificadores de los alimentos a resolver.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Diccionario de identificador a datos del alimento.</returns>
    Task<IReadOnlyDictionary<Guid, FoodNameInfo>> GetFoodNamesAsync(
        IReadOnlyCollection<Guid> foodIds,
        CancellationToken ct = default);
}
