namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Configuración del paciente de volumen de desarrollo, con historial grande para probar la app en un
/// celular. Se vincula a la sección <c>VolumePatient</c>. La contraseña vive solo en user-secrets.
/// </summary>
public sealed class VolumePatientOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "VolumePatient";

    /// <summary>
    /// Indica si el sembrado del paciente de volumen está habilitado.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Correo electrónico del paciente (también su nombre de usuario en Keycloak).
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Nombre completo del paciente.
    /// </summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Contraseña permanente. Nunca se escribe en logs.
    /// </summary>
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Interruptores del paciente de volumen que solo se aceptan por variable de entorno: los user-secrets
/// ganan a las variables de entorno, así que una clave olvidada ahí no debe poder activarlos.
/// </summary>
/// <param name="Regenerate">Borra el historial del paciente y lo vuelve a sembrar con fechas de hoy.</param>
/// <param name="ProbeFoodRetired">Desactiva el alimento de prueba.</param>
public sealed record VolumePatientSwitches(bool Regenerate, bool ProbeFoodRetired)
{
    /// <summary>
    /// Variable de entorno que pide regenerar el historial.
    /// </summary>
    public const string RegenerateVariable = "VolumePatient__Regenerate";

    /// <summary>
    /// Variable de entorno que pide retirar el alimento de prueba.
    /// </summary>
    public const string ProbeFoodRetiredVariable = "VolumePatient__ProbeFoodRetired";

    /// <summary>
    /// Lee los interruptores de las variables de entorno del proceso.
    /// </summary>
    public static VolumePatientSwitches FromEnvironment() => new(
        IsTrue(Environment.GetEnvironmentVariable(RegenerateVariable)),
        IsTrue(Environment.GetEnvironmentVariable(ProbeFoodRetiredVariable)));

    private static bool IsTrue(string? value) => bool.TryParse(value, out var parsed) && parsed;
}
