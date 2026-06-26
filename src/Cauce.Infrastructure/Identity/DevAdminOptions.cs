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
    /// Contraseña temporal del nutricionista de prueba.
    /// </summary>
    public string TemporaryPassword { get; init; } = string.Empty;
}
