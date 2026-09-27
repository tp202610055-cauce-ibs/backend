namespace Cauce.Application.Common.Identity;

/// <summary>
/// Causas internas de un intento de autenticación rechazado. Se guardan en <c>audit_logs</c> y nunca
/// viajan al cliente, que recibe siempre la misma respuesta genérica (acta A68). Salen del estado local
/// de la cuenta y de la Admin API de Keycloak, no del texto de sus errores: describen el estado de la
/// cuenta en el momento del fallo, que es lo que permite investigar un intento.
/// </summary>
public static class AuthFailureCauses
{
    /// <summary>
    /// La cuenta existe y puede iniciar sesión: la contraseña no coincide.
    /// </summary>
    public const string WrongPassword = "wrong_password";

    /// <summary>
    /// El correo no corresponde a ninguna cuenta local.
    /// </summary>
    public const string UnknownAccount = "unknown_account";

    /// <summary>
    /// La cuenta está deshabilitada en Keycloak.
    /// </summary>
    public const string AccountDisabled = "account_disabled";

    /// <summary>
    /// La cuenta local sigue pendiente de activación.
    /// </summary>
    public const string PendingActivation = "pending_activation";

    /// <summary>
    /// Keycloak exige completar una acción antes de iniciar sesión, como cambiar una contraseña temporal.
    /// </summary>
    public const string RequiredActionPending = "required_action_pending";

    /// <summary>
    /// La cuenta está bloqueada por intentos fallidos consecutivos.
    /// </summary>
    public const string AccountLocked = "account_locked";

    /// <summary>
    /// Las credenciales son válidas pero el rol no puede usar el canal, como un paciente en el portal.
    /// </summary>
    public const string RoleNotAllowed = "role_not_allowed";

    /// <summary>
    /// La cuenta local está suspendida.
    /// </summary>
    public const string AccountSuspended = "account_suspended";

    /// <summary>
    /// La cuenta local está inactiva.
    /// </summary>
    public const string AccountInactive = "account_inactive";

    /// <summary>
    /// La ruta del canal recibió un cliente OIDC que no le corresponde.
    /// </summary>
    public const string UnsupportedClient = "unsupported_client";

    /// <summary>
    /// Keycloak rechazó al cliente OIDC del backend: falta el secret, es incorrecto o el cliente no admite
    /// el grant pedido. Es un error de configuración, no de las credenciales del usuario.
    /// </summary>
    public const string ClientMisconfigured = "client_misconfigured";

    /// <summary>
    /// Keycloak autenticó al usuario, pero no existe su cuenta local.
    /// </summary>
    public const string LocalAccountMissing = "local_account_missing";

    /// <summary>
    /// La petición no pasó la validación de entrada.
    /// </summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>
    /// El portal pidió renovar la sesión sin la cookie del refresh token.
    /// </summary>
    public const string MissingRefreshCookie = "missing_refresh_cookie";

    /// <summary>
    /// Keycloak rechazó el refresh token: venció, se revocó o ya se usó.
    /// </summary>
    public const string InvalidRefreshToken = "invalid_refresh_token";

    /// <summary>
    /// El portal llamó a una ruta de sesión sin el header propio que la defiende de CSRF.
    /// </summary>
    public const string CsrfHeaderMissing = "csrf_header_missing";

    /// <summary>
    /// No se pudo determinar la causa, por ejemplo porque la Admin API no respondió.
    /// </summary>
    public const string Undetermined = "undetermined";
}
