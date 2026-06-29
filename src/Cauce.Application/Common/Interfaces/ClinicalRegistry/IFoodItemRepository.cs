using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Repositorio del agregado <see cref="FoodItem"/> (catálogo de alimentos).
/// </summary>
public interface IFoodItemRepository
{
    /// <summary>
    /// Busca un alimento por su identificador.
    /// </summary>
    /// <param name="foodId">Identificador del alimento.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El alimento o <see langword="null"/> si no existe.</returns>
    Task<FoodItem?> FindByIdAsync(Guid foodId, CancellationToken ct = default);

    /// <summary>
    /// Lista los alimentos activos de forma paginada, con filtros opcionales.
    /// </summary>
    /// <param name="skip">Cantidad de elementos a omitir.</param>
    /// <param name="take">Cantidad de elementos a tomar.</param>
    /// <param name="categoryFilter">Filtro por categoría, opcional.</param>
    /// <param name="fodmapFilter">Filtro por nivel FODMAP, opcional.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La página de alimentos activos.</returns>
    Task<IReadOnlyList<FoodItem>> ListActiveAsync(int skip, int take, string? categoryFilter, FodmapLevel? fodmapFilter, CancellationToken ct = default);

    /// <summary>
    /// Busca alimentos activos por coincidencia de nombre.
    /// </summary>
    /// <param name="query">Texto a buscar en el nombre.</param>
    /// <param name="take">Cantidad máxima de resultados.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los alimentos coincidentes.</returns>
    Task<IReadOnlyList<FoodItem>> SearchByNameAsync(string query, int take, CancellationToken ct = default);

    /// <summary>
    /// Cuenta los alimentos activos que cumplen los filtros indicados.
    /// </summary>
    /// <param name="categoryFilter">Filtro por categoría, opcional.</param>
    /// <param name="fodmapFilter">Filtro por nivel FODMAP, opcional.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La cantidad de alimentos activos.</returns>
    Task<int> CountActiveAsync(string? categoryFilter, FodmapLevel? fodmapFilter, CancellationToken ct = default);

    /// <summary>
    /// Indica si existe un alimento con el nombre dado.
    /// </summary>
    /// <param name="name">Nombre del alimento.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si existe.</returns>
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Agrega un nuevo alimento al contexto de persistencia.
    /// </summary>
    /// <param name="foodItem">Alimento a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(FoodItem foodItem, CancellationToken ct = default);
}
