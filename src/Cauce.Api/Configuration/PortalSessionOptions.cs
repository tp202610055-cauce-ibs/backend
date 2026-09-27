namespace Cauce.Api.Configuration;

/// <summary>
/// Configuración de la sesión del portal web (acta A68): la cookie que lleva el refresh token y el header
/// que exigen la renovación y el cierre de sesión como defensa contra CSRF. Se vincula a la sección
/// <c>PortalSession</c>.
/// </summary>
public sealed class PortalSessionOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "PortalSession";

    /// <summary>
    /// Nombre de la cookie que lleva el refresh token del portal.
    /// </summary>
    public const string CookieName = "cauce_portal_rt";

    /// <summary>
    /// Ruta de la cookie. La acota a las rutas de sesión del portal, así que el navegador no la envía en
    /// ninguna otra petición a la API.
    /// </summary>
    public const string CookiePath = "/api/v1/auth/portal";

    /// <summary>
    /// Header que el portal envía en la renovación y en el cierre de sesión. Un formulario de otro sitio
    /// no puede agregar headers propios, y un <c>fetch</c> de otro origen que lo intente dispara un
    /// preflight que CORS rechaza.
    /// </summary>
    public const string CsrfHeaderName = "X-Cauce-Portal";

    /// <summary>
    /// Indica si la cookie se marca <c>Secure</c>. Es <see langword="true"/> por defecto y solo se apaga
    /// en desarrollo, donde la API corre sobre <c>http://localhost</c>.
    /// </summary>
    public bool CookieSecure { get; init; } = true;
}
