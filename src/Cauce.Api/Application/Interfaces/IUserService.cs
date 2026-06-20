using Cauce.Api.Application.DTOs.Users;
using Cauce.Api.Application.Exceptions;

namespace Cauce.Api.Application.Interfaces;

/// <summary>
/// Servicio de aplicación para consultas y operaciones sobre el perfil de usuario.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Obtiene el perfil público del usuario indicado.
    /// </summary>
    /// <param name="userId">Identificador del usuario a consultar.</param>
    /// <returns>El perfil del usuario sin datos sensibles.</returns>
    /// <exception cref="UserNotFoundException">
    /// Se lanza si no existe un usuario con el identificador proporcionado.
    /// </exception>
    Task<UserProfileResponse> GetProfileAsync(Guid userId);
}