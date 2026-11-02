namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Estado del correo con el que se crea un usuario en Keycloak. Ninguno de los dos deja acciones pendientes.
/// </summary>
public enum NewUserEmailState
{
    /// <summary>
    /// Correo verificado: cuentas que provisiona el sistema (nutricionistas, administrador y semillas).
    /// </summary>
    Verified,

    /// <summary>
    /// Correo sin verificar: el paciente inicia sesión igual y la verificación queda como recordatorio.
    /// </summary>
    Unverified
}
