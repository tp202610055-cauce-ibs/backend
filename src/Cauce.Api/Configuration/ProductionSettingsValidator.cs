namespace Cauce.Api.Configuration;

/// <summary>
/// Comprueba al arrancar en Production que la configuración obligatoria existe, para fallar en el arranque y
/// no en la primera petición. Los mensajes nombran la clave como variable de entorno, nunca su valor.
/// </summary>
internal static class ProductionSettingsValidator
{
    /// <summary>
    /// Claves que no pueden faltar ni estar vacías en Production.
    /// </summary>
    internal static readonly IReadOnlyList<string> RequiredKeys =
    [
        "ConnectionStrings:Cauce",
        "Keycloak:Authority",
        "Keycloak:ClientSecret",
        "Keycloak:WebPortalClientSecret",
        "KeyDb:ConnectionString",
        "Storage:Minio:Endpoint",
        "Storage:Minio:AccessKey",
        "Storage:Minio:SecretKey",
        "Storage:Minio:PublicEndpoint",
        "Email:SmtpHost",
        "Email:FromAddress",
        "Email:PortalAppBaseUrl",
        "Email:MobileAppBaseUrl",
        "AdminApi:Value",
        "Cors:AllowedOrigins"
    ];

    private const string FcmUseFakeKey = "Notifications:Fcm:UseFake";
    private const string FcmCredentialsPathKey = "Notifications:Fcm:CredentialsPath";

    /// <summary>
    /// Lanza una excepción que enumera todos los problemas encontrados, si hay alguno.
    /// </summary>
    /// <param name="configuration">Configuración de la aplicación.</param>
    public static void EnsureValid(IConfiguration configuration)
    {
        var problems = FindProblems(configuration);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "La configuración de Production está incompleta: " + string.Join(" ", problems));
        }
    }

    /// <summary>
    /// Devuelve un mensaje por cada clave faltante y por el archivo de Firebase si el push real está activo.
    /// </summary>
    /// <param name="configuration">Configuración de la aplicación.</param>
    internal static IReadOnlyList<string> FindProblems(IConfiguration configuration)
    {
        var problems = RequiredKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .Select(key => $"Falta {AsEnvironmentVariable(key)}.")
            .ToList();

        if (configuration.GetValue(FcmUseFakeKey, true))
        {
            return problems;
        }

        var credentialsPath = configuration[FcmCredentialsPathKey];
        if (string.IsNullOrWhiteSpace(credentialsPath))
        {
            problems.Add($"Falta {AsEnvironmentVariable(FcmCredentialsPathKey)}, obligatoria con el push real.");
        }
        else if (!CanRead(credentialsPath))
        {
            problems.Add(
                $"No se puede leer el archivo de {AsEnvironmentVariable(FcmCredentialsPathKey)} ({credentialsPath}).");
        }

        return problems;
    }

    private static bool CanRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string AsEnvironmentVariable(string key) => key.Replace(":", "__", StringComparison.Ordinal);
}
