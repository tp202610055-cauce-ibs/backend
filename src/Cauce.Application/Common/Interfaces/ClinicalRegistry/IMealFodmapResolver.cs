using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Resuelve el nivel FODMAP agregado de varias comidas a la vez, para que las consultas de historial
/// puedan devolverlo sin hacer una consulta al catálogo por cada ítem.
/// </summary>
public interface IMealFodmapResolver
{
    /// <summary>
    /// Calcula el nivel FODMAP agregado de cada comida indicada.
    /// </summary>
    /// <param name="meals">Comidas con sus ítems ya cargados.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Diccionario de identificador de comida a nivel FODMAP agregado.</returns>
    Task<IReadOnlyDictionary<Guid, FodmapLevel>> ResolveAsync(
        IReadOnlyCollection<Meal> meals,
        CancellationToken ct = default);
}
