using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;

namespace Cauce.Application.Notifications.UseCases;

/// <summary>
/// Resuelve el nutricionista autenticado a partir del JWT para los casos de uso de la bandeja.
/// </summary>
internal static class NotificationsUserContext
{
    public static async Task<Guid> ResolveNutritionistIdAsync(
        ICurrentUserService currentUser,
        IUserRepository userRepository,
        CancellationToken ct)
    {
        var keycloakId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var nutritionistRoleId = await userRepository.GetRoleIdAsync(UserRoles.Nutritionist, ct).ConfigureAwait(false);
        if (user.RoleId != nutritionistRoleId)
        {
            throw new UnauthorizedAccessException("Solo un nutricionista tiene bandeja de avisos.");
        }

        return user.Id;
    }
}
