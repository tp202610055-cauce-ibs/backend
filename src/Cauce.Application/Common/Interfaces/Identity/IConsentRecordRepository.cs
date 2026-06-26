using Cauce.Domain.Identity;

namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Repositorio del agregado <see cref="ConsentRecord"/>.
/// </summary>
public interface IConsentRecordRepository
{
    /// <summary>
    /// Busca el consentimiento vigente de un usuario.
    /// </summary>
    /// <param name="userId">Identificador del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El consentimiento vigente o <see langword="null"/> si no hay.</returns>
    Task<ConsentRecord?> FindCurrentForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Agrega un nuevo registro de consentimiento al contexto de persistencia.
    /// </summary>
    /// <param name="record">Registro a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(ConsentRecord record, CancellationToken ct = default);

    /// <summary>
    /// Marca como no vigente el consentimiento actual del usuario, si existe.
    /// </summary>
    /// <param name="userId">Identificador del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task SupersedeCurrentAsync(Guid userId, CancellationToken ct = default);
}
