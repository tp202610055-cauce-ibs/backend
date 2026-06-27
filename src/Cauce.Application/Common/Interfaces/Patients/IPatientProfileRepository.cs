using Cauce.Domain.Patients;

namespace Cauce.Application.Common.Interfaces.Patients;

/// <summary>
/// Repositorio del agregado <see cref="PatientProfile"/>.
/// </summary>
public interface IPatientProfileRepository
{
    /// <summary>
    /// Busca el perfil clínico de un usuario.
    /// </summary>
    /// <param name="userId">Identificador de la cuenta de usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El perfil o <see langword="null"/> si no existe.</returns>
    Task<PatientProfile?> FindByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Agrega un nuevo perfil al contexto de persistencia.
    /// </summary>
    /// <param name="profile">Perfil a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(PatientProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Indica si ya existe un perfil para el usuario dado.
    /// </summary>
    /// <param name="userId">Identificador de la cuenta de usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si existe.</returns>
    Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken ct = default);
}
