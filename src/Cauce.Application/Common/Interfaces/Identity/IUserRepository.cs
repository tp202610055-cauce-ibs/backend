using Cauce.Domain.Identity;

namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Repositorio del agregado <see cref="User"/>.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Busca un usuario por su identificador.
    /// </summary>
    /// <param name="userId">Identificador del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El usuario o <see langword="null"/> si no existe.</returns>
    Task<User?> FindByIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Busca un usuario por su correo electrónico.
    /// </summary>
    /// <param name="email">Correo electrónico.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El usuario o <see langword="null"/> si no existe.</returns>
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Busca un usuario por su identificador de Keycloak.
    /// </summary>
    /// <param name="keycloakId">Identificador del usuario en Keycloak.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El usuario o <see langword="null"/> si no existe.</returns>
    Task<User?> FindByKeycloakIdAsync(string keycloakId, CancellationToken ct = default);

    /// <summary>
    /// Agrega un nuevo usuario al contexto de persistencia.
    /// </summary>
    /// <param name="user">Usuario a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Indica si ya existe un usuario con el correo electrónico dado.
    /// </summary>
    /// <param name="email">Correo electrónico.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si existe.</returns>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el identificador del rol del catálogo a partir de su nombre.
    /// </summary>
    /// <param name="roleName">Nombre del rol.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador del rol.</returns>
    Task<int> GetRoleIdAsync(string roleName, CancellationToken ct = default);
}
