using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;

namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Regla única de activación de las cuentas de nutricionista (acta A51). El nutricionista se provisiona en
/// <see cref="UserStatus.PendingActivation"/> y sin contraseña; autenticarse prueba que la definió él mismo
/// con el enlace que envía Keycloak, así que su primera autenticación lo activa.
/// </summary>
/// <remarks>
/// La invocan dos puntos de entrada: el handler de login, porque en esa petición todavía no hay un token
/// que inspeccionar, y un behavior del pipeline para el resto de las peticiones autenticadas. Concentrar la
/// regla aquí evita dos implementaciones que puedan divergir con el tiempo.
/// <para>
/// No persiste: la activación y su fila de auditoría se enrolan en el <c>ChangeTracker</c> y las confirma
/// el <c>SaveChangesAsync</c> del llamador.
/// </para>
/// </remarks>
public interface INutritionistActivationService
{
    /// <summary>
    /// Activa la cuenta y audita la transición si pertenece a un nutricionista pendiente de activación.
    /// En cualquier otro caso no hace nada.
    /// </summary>
    /// <param name="user">Cuenta local ya resuelta y rastreada por el contexto.</param>
    /// <param name="trigger">Punto de entrada que dispara la activación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <see langword="true"/> si la cuenta se activó; <see langword="false"/> si no correspondía.
    /// </returns>
    Task<bool> ActivateIfPendingAsync(
        User user,
        NutritionistActivationTrigger trigger,
        CancellationToken ct = default);
}
