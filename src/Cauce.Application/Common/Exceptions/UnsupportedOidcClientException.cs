namespace Cauce.Application.Common.Exceptions;

/// <summary>
/// Se lanza cuando una ruta de sesión recibe un cliente OIDC que no corresponde a su canal: las rutas
/// del móvil solo aceptan <c>cauce-mobile</c> y las del portal solo <c>cauce-web-portal</c> (acta A68).
/// Sin esta restricción, <c>/auth/login</c> con el cliente del portal devolvería el refresh token en el
/// cuerpo y saltearía el filtro de rol del portal.
/// </summary>
public sealed class UnsupportedOidcClientException : Exception
{
    /// <summary>
    /// Inicializa la excepción.
    /// </summary>
    public UnsupportedOidcClientException()
        : base("El cliente indicado no está admitido en esta ruta.")
    {
    }
}
