using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;

namespace Cauce.Application.Recommendations.UseCases;

/// <summary>
/// Resuelve la identidad local (clave primaria de <c>users</c>) del usuario autenticado a
/// partir del JWT de Keycloak, validando además su rol. Centraliza el patrón usado por los
/// handlers del módulo (ver <c>CreateSymptomCommandHandler</c> en el registro clínico).
/// </summary>
internal static class RecommendationsUserContext
{
    /// <summary>
    /// Resuelve el identificador local del paciente autenticado.
    /// </summary>
    /// <param name="currentUser">Servicio del usuario autenticado.</param>
    /// <param name="userRepository">Repositorio de usuarios.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador local del paciente.</returns>
    /// <exception cref="UnauthorizedAccessException">Si no hay usuario autenticado, no tiene cuenta local o no es paciente.</exception>
    public static Task<Guid> ResolvePatientIdAsync(
        ICurrentUserService currentUser,
        IUserRepository userRepository,
        CancellationToken ct)
    {
        return ResolveForRoleAsync(currentUser, userRepository, UserRoles.Patient, "Solo un paciente puede realizar esta operación.", ct);
    }

    /// <summary>
    /// Resuelve el identificador local del nutricionista autenticado.
    /// </summary>
    /// <param name="currentUser">Servicio del usuario autenticado.</param>
    /// <param name="userRepository">Repositorio de usuarios.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador local del nutricionista.</returns>
    /// <exception cref="UnauthorizedAccessException">Si no hay usuario autenticado, no tiene cuenta local o no es nutricionista.</exception>
    public static Task<Guid> ResolveNutritionistIdAsync(
        ICurrentUserService currentUser,
        IUserRepository userRepository,
        CancellationToken ct)
    {
        return ResolveForRoleAsync(currentUser, userRepository, UserRoles.Nutritionist, "Solo un nutricionista puede realizar esta operación.", ct);
    }

    private static async Task<Guid> ResolveForRoleAsync(
        ICurrentUserService currentUser,
        IUserRepository userRepository,
        string roleName,
        string roleErrorMessage,
        CancellationToken ct)
    {
        var keycloakId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var roleId = await userRepository.GetRoleIdAsync(roleName, ct).ConfigureAwait(false);
        if (user.RoleId != roleId)
        {
            throw new UnauthorizedAccessException(roleErrorMessage);
        }

        return user.Id;
    }
}
