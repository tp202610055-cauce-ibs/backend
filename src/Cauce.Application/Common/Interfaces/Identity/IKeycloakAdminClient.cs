namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Cliente de la Admin API de Keycloak. Encapsula la gestión de credenciales y
/// usuarios delegada al proveedor de identidad. Toda la administración de
/// contraseñas vive en Keycloak; el backend nunca las almacena.
/// </summary>
public interface IKeycloakAdminClient
{
    /// <summary>
    /// Crea un usuario en Keycloak con el rol del realm indicado.
    /// </summary>
    /// <param name="email">Correo electrónico del usuario.</param>
    /// <param name="fullName">Nombre completo del usuario.</param>
    /// <param name="roleName">Nombre del rol del realm a asignar.</param>
    /// <param name="requireEmailVerification">
    /// Si es <see langword="true"/>, el usuario se crea con el correo sin verificar
    /// y con la required action <c>VERIFY_EMAIL</c>; si es <see langword="false"/>,
    /// se crea con el correo verificado.
    /// </param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador del usuario creado en Keycloak.</returns>
    Task<string> CreateUserAsync(
        string email,
        string fullName,
        string roleName,
        bool requireEmailVerification,
        CancellationToken ct = default);

    /// <summary>
    /// Asigna una contraseña temporal que obliga al usuario a cambiarla en el
    /// próximo inicio de sesión (required action <c>UPDATE_PASSWORD</c>).
    /// </summary>
    /// <param name="keycloakUserId">Identificador del usuario en Keycloak.</param>
    /// <param name="password">Contraseña temporal.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task SetTemporaryPasswordAsync(
        string keycloakUserId,
        string password,
        CancellationToken ct = default);

    /// <summary>
    /// Establece una contraseña permanente para el usuario.
    /// </summary>
    /// <param name="keycloakUserId">Identificador del usuario en Keycloak.</param>
    /// <param name="newPassword">Nueva contraseña permanente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task ResetPasswordAsync(
        string keycloakUserId,
        string newPassword,
        CancellationToken ct = default);

    /// <summary>
    /// Dispara el envío del correo de verificación de cuenta de Keycloak.
    /// </summary>
    /// <param name="keycloakUserId">Identificador del usuario en Keycloak.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task SendVerifyEmailAsync(
        string keycloakUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina un usuario de Keycloak. Se usa como compensación cuando la
    /// persistencia local falla tras crear el usuario.
    /// </summary>
    /// <param name="keycloakUserId">Identificador del usuario en Keycloak.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task DeleteUserAsync(
        string keycloakUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Deshabilita un usuario en Keycloak (<c>enabled: false</c>) sin eliminarlo. Se prefiere sobre la
    /// eliminación física para preservar la trazabilidad de auditoría exigida por la Ley N° 29733 al
    /// anonimizar una cuenta de paciente (US26, acta A17). Un usuario deshabilitado no puede iniciar
    /// sesión.
    /// </summary>
    /// <param name="keycloakUserId">Identificador del usuario en Keycloak.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task DisableUserAsync(
        string keycloakUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Busca un usuario por correo electrónico exacto.
    /// </summary>
    /// <param name="email">Correo electrónico a buscar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El usuario encontrado o <see langword="null"/> si no existe.</returns>
    Task<KeycloakUserDto?> FindByEmailAsync(
        string email,
        CancellationToken ct = default);
}

/// <summary>
/// Representación mínima de un usuario de Keycloak.
/// </summary>
/// <param name="Id">Identificador del usuario en Keycloak.</param>
/// <param name="Email">Correo electrónico.</param>
/// <param name="EmailVerified">Indica si el correo está verificado.</param>
public sealed record KeycloakUserDto(string Id, string Email, bool EmailVerified);
