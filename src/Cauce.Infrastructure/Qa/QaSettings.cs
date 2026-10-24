using Cauce.Infrastructure.Qa.Manifest;
using Microsoft.Extensions.Configuration;

namespace Cauce.Infrastructure.Qa;

/// <summary>
/// Variables de entorno de los comandos de QA. Las contraseñas solo se leen de aquí y nunca se imprimen.
/// </summary>
public sealed class QaSettings
{
    /// <summary>
    /// Variable que habilita los comandos de QA.
    /// </summary>
    public const string ModeKey = "QA_MODE";

    /// <summary>
    /// Variable que habilita los comandos que borran o recrean cuentas.
    /// </summary>
    public const string AllowDestructiveKey = "QA_ALLOW_DESTRUCTIVE";

    /// <summary>
    /// Contraseña de las cuentas de paciente del manifiesto.
    /// </summary>
    public const string PatientPasswordKey = "QA_PATIENT_PASSWORD";

    /// <summary>
    /// Contraseña de las cuentas de nutricionista del manifiesto.
    /// </summary>
    public const string NutritionistPasswordKey = "QA_NUTRITIONIST_PASSWORD";

    /// <summary>
    /// Contraseña de las cuentas de humo del portal.
    /// </summary>
    public const string SmokePasswordKey = "QA_SMOKE_PASSWORD";

    private readonly IConfiguration _configuration;

    /// <summary>
    /// Lee las variables desde la configuración de la aplicación, que incluye las variables de entorno.
    /// </summary>
    public QaSettings(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Falla si <c>QA_MODE</c> no vale 1.
    /// </summary>
    /// <exception cref="QaGuardException">Si el modo QA no está habilitado.</exception>
    public void EnsureQaMode()
    {
        if (!IsOne(ModeKey))
        {
            throw new QaGuardException($"Los comandos de QA exigen {ModeKey}=1.");
        }
    }

    /// <summary>
    /// Falla si <c>QA_MODE</c> o <c>QA_ALLOW_DESTRUCTIVE</c> no valen 1.
    /// </summary>
    /// <exception cref="QaGuardException">Si falta alguna de las dos.</exception>
    public void EnsureDestructiveAllowed()
    {
        EnsureQaMode();
        if (!IsOne(AllowDestructiveKey))
        {
            throw new QaGuardException($"Este comando borra o recrea cuentas y exige además {AllowDestructiveKey}=1.");
        }
    }

    /// <summary>
    /// Contraseña de la cuenta según su origen y su rol.
    /// </summary>
    /// <exception cref="QaGuardException">Si la variable correspondiente no está definida.</exception>
    public string PasswordFor(QaRole role, bool smoke)
    {
        var key = PasswordKeyFor(role, smoke);
        var value = _configuration[key];
        if (string.IsNullOrEmpty(value))
        {
            throw new QaGuardException($"Falta la variable {key}.");
        }

        return value;
    }

    /// <summary>
    /// Nombre de la variable que guarda la contraseña de un rol.
    /// </summary>
    public static string PasswordKeyFor(QaRole role, bool smoke) =>
        smoke ? SmokePasswordKey : role == QaRole.Patient ? PatientPasswordKey : NutritionistPasswordKey;

    private bool IsOne(string key) => string.Equals(_configuration[key]?.Trim(), "1", StringComparison.Ordinal);
}

/// <summary>
/// El comando de QA terminó y encontró fallas que ya informó. El proceso termina con el código 1, sin traza.
/// </summary>
public sealed class QaFailureException : Exception
{
    /// <summary>
    /// Crea la excepción con el resumen de las fallas.
    /// </summary>
    public QaFailureException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// El entorno no permite ejecutar el comando de QA. El proceso termina con el código 2, sin escribir nada.
/// </summary>
public sealed class QaGuardException : Exception
{
    /// <summary>
    /// Crea la excepción con un mensaje para el operador.
    /// </summary>
    public QaGuardException(string message)
        : base(message)
    {
    }
}
