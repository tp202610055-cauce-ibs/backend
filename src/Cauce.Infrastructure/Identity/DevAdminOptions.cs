namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Configuración del nutricionista de prueba sembrado automáticamente en el
/// entorno de desarrollo. Se vincula a la sección <c>DevAdmin</c>.
/// </summary>
public sealed class DevAdminOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "DevAdmin";

    /// <summary>
    /// Indica si el sembrado del nutricionista de prueba está habilitado.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Correo electrónico del nutricionista de prueba.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Nombre completo del nutricionista de prueba.
    /// </summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Contraseña permanente del nutricionista de prueba. Es permanente porque el portal inicia sesión
    /// a través del backend y nunca muestra una pantalla de Keycloak donde cambiar una temporal (acta A68).
    /// Vive solo en la configuración de desarrollo y nunca se registra en los logs.
    /// </summary>
    public string Password { get; init; } = string.Empty;
}
