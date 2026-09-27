namespace Cauce.Application.Common.Identity;

/// <summary>
/// Identificadores canónicos de los clientes OIDC del realm <c>cauce</c>. Se usan en lugar de
/// literales sueltos allí donde el comportamiento depende del cliente que originó la petición, como
/// el destino del enlace de restablecimiento de contraseña.
/// </summary>
public static class OidcClients
{
    /// <summary>
    /// App móvil Flutter para pacientes. Cliente público con Direct Access Grants.
    /// </summary>
    public const string Mobile = "cauce-mobile";

    /// <summary>
    /// Portal web React para nutricionistas. Cliente confidencial con Direct Access Grants: el backend
    /// pide sus tokens con el secret, que nunca sale de la configuración del servidor (acta A68).
    /// </summary>
    public const string WebPortal = "cauce-web-portal";

    /// <summary>
    /// Indica si el identificador corresponde a un cliente OIDC conocido del realm.
    /// </summary>
    /// <param name="clientId">Identificador a evaluar.</param>
    /// <returns><see langword="true"/> si es un cliente conocido.</returns>
    public static bool IsKnown(string? clientId)
    {
        return clientId is Mobile or WebPortal;
    }

    /// <summary>
    /// Devuelve el único cliente OIDC admitido en un canal de sesión. Las rutas del móvil solo aceptan
    /// <see cref="Mobile"/> y las del portal solo <see cref="WebPortal"/> (acta A68).
    /// </summary>
    /// <param name="channel">Canal de la petición.</param>
    /// <returns>El identificador del cliente OIDC del canal.</returns>
    public static string ForChannel(LoginChannel channel)
    {
        return channel == LoginChannel.Portal ? WebPortal : Mobile;
    }
}
