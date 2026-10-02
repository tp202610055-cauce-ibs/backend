using Cauce.Application.Common.Interfaces.Identity;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Common.Identity;

/// <summary>
/// Compensación de un alta en Keycloak que no pudo completarse: borra el usuario recién creado para que no
/// quede una identidad sin cuenta local que después bloquee un nuevo registro con el mismo correo (acta
/// A70). La comparten el registro de pacientes, la provisión de nutricionistas y el propio cliente de
/// Keycloak cuando falla la asignación del rol.
/// </summary>
public static class KeycloakCompensation
{
    /// <summary>
    /// Tope de tiempo propio de la compensación. No hereda la cancelación de la petición: si el cliente
    /// cortó la conexión, esa es justamente la falla que hay que compensar, y con un token ya cancelado el
    /// borrado ni siquiera saldría.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Intenta borrar el usuario de Keycloak. Nunca lanza: una compensación fallida queda registrada para la
    /// limpieza manual y la excepción original sigue su curso en el llamador.
    /// </summary>
    /// <param name="keycloakAdminClient">Cliente de la Admin API de Keycloak.</param>
    /// <param name="keycloakUserId">Identificador del usuario recién creado en Keycloak.</param>
    /// <param name="logger">Logger del llamador.</param>
    /// <param name="timeout">Tope de tiempo; si se omite, <see cref="Timeout"/>.</param>
    /// <returns><see langword="true"/> si Keycloak confirmó el borrado.</returns>
    public static async Task<bool> TryDeleteUserAsync(
        IKeycloakAdminClient keycloakAdminClient,
        string keycloakUserId,
        ILogger logger,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(keycloakAdminClient);
        ArgumentNullException.ThrowIfNull(logger);

        using var timeoutSource = new CancellationTokenSource(timeout ?? Timeout);
        try
        {
            await keycloakAdminClient.DeleteUserAsync(keycloakUserId, timeoutSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            // El identificador de Keycloak no es un dato personal y es lo que necesita el procedimiento de
            // limpieza manual para ubicar al usuario huérfano (acta A70).
            logger.LogCritical(
                exception,
                "Compensation failed: Keycloak user {KeycloakUserId} could not be deleted after a failed provisioning; remove it manually.",
                keycloakUserId);
            return false;
        }
    }
}
