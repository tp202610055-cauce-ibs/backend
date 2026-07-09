namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Configuración del paciente de prueba sembrado automáticamente en el entorno de desarrollo. Se
/// vincula a la sección <c>DemoPatient</c>. Permite validar el happy-path autenticado de la app móvil
/// (login por Direct Access Grants → perfil → registro clínico) sin depender del flujo de invitación por
/// nutricionista ni de la verificación por correo. Solo debe poblarse en Development.
/// </summary>
public sealed class DemoPatientOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "DemoPatient";

    /// <summary>
    /// Indica si el sembrado del paciente de prueba está habilitado.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Correo electrónico del paciente de prueba (también su nombre de usuario en Keycloak).
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Nombre completo del paciente de prueba.
    /// </summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Contraseña permanente del paciente de prueba. Se establece en Keycloak sin la required action
    /// <c>UPDATE_PASSWORD</c> para permitir el inicio de sesión por Direct Access Grants. Nunca se
    /// escribe en logs; vive únicamente en la configuración y en la documentación de usuarios de desarrollo.
    /// </summary>
    public string Password { get; init; } = string.Empty;
}
