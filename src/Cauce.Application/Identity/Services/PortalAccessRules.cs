using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;

namespace Cauce.Application.Identity.Services;

/// <summary>
/// Reglas de acceso del canal portal, compartidas por el inicio de sesión y la renovación (acta A68).
/// Keycloak ya validó las credenciales cuando se evalúan: lo que se decide aquí es si la cuenta, aun
/// autenticada, puede usar el portal.
/// </summary>
public static class PortalAccessRules
{
    /// <summary>
    /// Devuelve la causa por la que la cuenta no puede usar el portal, o <see langword="null"/> si puede.
    /// Solo entra el rol nutricionista y, entre nutricionistas, se rechaza a los suspendidos e inactivos.
    /// Una cuenta pendiente de activación sí entra: su primer inicio de sesión es justamente el que la
    /// activa (acta A51).
    /// </summary>
    /// <param name="user">Cuenta local ya autenticada por Keycloak.</param>
    /// <param name="userRepository">Repositorio de usuarios, para resolver el nombre del rol.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La causa del rechazo, una de <see cref="AuthFailureCauses"/>, o <see langword="null"/>.</returns>
    public static async Task<string?> FindRejectionCauseAsync(
        User user,
        IUserRepository userRepository,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(userRepository);

        var roleName = await userRepository.GetRoleNameAsync(user.RoleId, ct).ConfigureAwait(false);
        if (!string.Equals(roleName, UserRoles.Nutritionist, StringComparison.Ordinal))
        {
            return AuthFailureCauses.RoleNotAllowed;
        }

        return user.Status switch
        {
            UserStatus.Suspended => AuthFailureCauses.AccountSuspended,
            UserStatus.Inactive => AuthFailureCauses.AccountInactive,
            _ => null
        };
    }
}
