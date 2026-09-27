namespace Cauce.Application.Common.Identity;

/// <summary>
/// Canal por el que un cliente abre, renueva o cierra una sesión contra el backend. Cada canal tiene
/// su cliente OIDC y sus rutas: el móvil usa <c>/auth/login</c>, <c>/auth/refresh</c> y
/// <c>/auth/logout</c>; el portal usa las mismas operaciones bajo <c>/auth/portal</c> (acta A68).
/// </summary>
public enum LoginChannel
{
    /// <summary>
    /// App móvil de pacientes, con el cliente <c>cauce-mobile</c>. El refresh token viaja en el cuerpo.
    /// </summary>
    Mobile,

    /// <summary>
    /// Portal web de nutricionistas, con el cliente <c>cauce-web-portal</c>. El refresh token viaja en una
    /// cookie <c>HttpOnly</c> y nunca en el cuerpo.
    /// </summary>
    Portal
}
