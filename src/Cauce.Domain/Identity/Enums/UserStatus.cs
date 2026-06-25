namespace Cauce.Domain.Identity.Enums;

/// <summary>
/// Estado del ciclo de vida de una cuenta de usuario. En base de datos se
/// persiste como <c>varchar</c> en snake_case lowercase (por ejemplo,
/// <c>"pending_activation"</c>).
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// Cuenta creada pero pendiente de activación (típicamente a la espera de
    /// verificación de correo).
    /// </summary>
    PendingActivation = 0,

    /// <summary>
    /// Cuenta activa y habilitada para operar.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Cuenta inactiva (deshabilitada sin sanción).
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// Cuenta suspendida administrativamente.
    /// </summary>
    Suspended = 3
}
