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
    /// Portal web React para nutricionistas. Cliente confidencial.
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
}
