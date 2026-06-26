namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Configuración de la clave de API administrativa que protege los endpoints de
/// provisión. Se vincula a la sección <c>AdminApi</c> de la configuración.
/// </summary>
public sealed class AdminApiKeyOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "AdminApi";

    /// <summary>
    /// Valor de la clave de API administrativa. En producción proviene de la
    /// variable de entorno <c>CAUCE_ADMIN_API_KEY</c>.
    /// </summary>
    public required string Value { get; init; }
}
