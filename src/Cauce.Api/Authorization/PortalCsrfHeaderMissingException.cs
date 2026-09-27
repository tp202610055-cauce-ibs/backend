namespace Cauce.Api.Authorization;

/// <summary>
/// Se lanza cuando una ruta de sesión del portal que usa la cookie del refresh token no recibe el header
/// propio del portal (acta A68). Es la defensa contra CSRF: la cookie sola no alcanza para renovar ni
/// cerrar la sesión.
/// </summary>
public sealed class PortalCsrfHeaderMissingException : Exception
{
    /// <summary>
    /// Inicializa la excepción.
    /// </summary>
    public PortalCsrfHeaderMissingException()
        : base("Falta el header de la sesión del portal.")
    {
    }
}
