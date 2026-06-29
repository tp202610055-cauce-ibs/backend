using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Repositorio del agregado <see cref="CustomFood"/> (alimentos personalizados).
/// </summary>
public interface ICustomFoodRepository
{
    /// <summary>
    /// Busca un alimento personalizado por su identificador.
    /// </summary>
    /// <param name="customFoodId">Identificador del alimento personalizado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El alimento personalizado o <see langword="null"/> si no existe.</returns>
    Task<CustomFood?> FindByIdAsync(Guid customFoodId, CancellationToken ct = default);

    /// <summary>
    /// Busca un alimento personalizado con sus ingredientes cargados.
    /// </summary>
    /// <param name="customFoodId">Identificador del alimento personalizado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El alimento personalizado con ingredientes, o <see langword="null"/>.</returns>
    Task<CustomFood?> FindByIdWithIngredientsAsync(Guid customFoodId, CancellationToken ct = default);

    /// <summary>
    /// Lista los alimentos personalizados de un paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los alimentos personalizados del paciente.</returns>
    Task<IReadOnlyList<CustomFood>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Indica si el paciente ya tiene un alimento personalizado con el nombre dado.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="name">Nombre del alimento personalizado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si existe.</returns>
    Task<bool> ExistsByPatientAndNameAsync(Guid patientId, string name, CancellationToken ct = default);

    /// <summary>
    /// Indica si el alimento personalizado está referenciado por ítems de comida.
    /// </summary>
    /// <param name="customFoodId">Identificador del alimento personalizado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si está referenciado.</returns>
    Task<bool> IsReferencedByMealItemsAsync(Guid customFoodId, CancellationToken ct = default);

    /// <summary>
    /// Agrega un nuevo alimento personalizado al contexto de persistencia.
    /// </summary>
    /// <param name="customFood">Alimento personalizado a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(CustomFood customFood, CancellationToken ct = default);

    /// <summary>
    /// Marca un alimento personalizado para eliminación.
    /// </summary>
    /// <param name="customFood">Alimento personalizado a eliminar.</param>
    void Remove(CustomFood customFood);
}
