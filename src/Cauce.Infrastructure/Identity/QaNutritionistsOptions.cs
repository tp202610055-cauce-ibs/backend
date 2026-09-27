namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Configuración de las cuentas de nutricionista que el entorno de desarrollo siembra para los casos de
/// prueba de acceso al portal: una deshabilitada y otra pendiente de activación (acta A68). Se vincula a
/// la sección <c>QaNutritionists</c>.
/// </summary>
public sealed class QaNutritionistsOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "QaNutritionists";

    /// <summary>
    /// Indica si el sembrado de las cuentas de prueba está habilitado.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Contraseña permanente de la cuenta deshabilitada, para probar que ni siquiera con la contraseña
    /// correcta se puede entrar. La cuenta pendiente no tiene contraseña. Vive solo en la configuración de
    /// desarrollo y nunca se registra en los logs.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Correo de la cuenta deshabilitada en Keycloak y suspendida en el backend.
    /// </summary>
    public string InactiveEmail { get; init; } = string.Empty;

    /// <summary>
    /// Nombre completo de la cuenta deshabilitada.
    /// </summary>
    public string InactiveFullName { get; init; } = string.Empty;

    /// <summary>
    /// Correo de la cuenta pendiente de activación, sin contraseña.
    /// </summary>
    public string PendingEmail { get; init; } = string.Empty;

    /// <summary>
    /// Nombre completo de la cuenta pendiente de activación.
    /// </summary>
    public string PendingFullName { get; init; } = string.Empty;
}
