using Cauce.Domain.Recommendations;

namespace Cauce.Application.Common.Interfaces.Recommendations;

/// <summary>
/// Repositorio del agregado <see cref="ModelVersion"/>.
/// </summary>
public interface IModelVersionRepository
{
    /// <summary>
    /// Obtiene la versión de modelo activa, o <see langword="null"/> si no hay ninguna.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La versión activa o <see langword="null"/>.</returns>
    Task<ModelVersion?> GetActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Busca una versión de modelo por su identificador.
    /// </summary>
    /// <param name="versionId">Identificador de la versión.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La versión o <see langword="null"/> si no existe.</returns>
    Task<ModelVersion?> GetByIdAsync(Guid versionId, CancellationToken ct = default);

    /// <summary>
    /// Busca una versión de modelo por su nombre.
    /// </summary>
    /// <param name="versionName">Nombre de la versión.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La versión o <see langword="null"/> si no existe.</returns>
    Task<ModelVersion?> GetByNameAsync(string versionName, CancellationToken ct = default);

    /// <summary>
    /// Agrega una nueva versión de modelo al contexto de persistencia.
    /// </summary>
    /// <param name="modelVersion">Versión a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(ModelVersion modelVersion, CancellationToken ct = default);
}
