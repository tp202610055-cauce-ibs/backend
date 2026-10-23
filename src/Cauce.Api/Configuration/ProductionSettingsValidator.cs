namespace Cauce.Api.Configuration;

/// <summary>
/// Comprueba al arrancar en Production que la configuración obligatoria existe, para fallar en el arranque y
/// no en la primera petición. Los mensajes nombran la clave como variable de entorno, nunca su valor.
/// </summary>
/// <remarks>La credencial de Firebase no se revisa aquí: si falla, se registra y la API arranca sin push.</remarks>
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
    /// Devuelve un mensaje por cada clave obligatoria que falta o está vacía.
    /// </summary>
    /// <param name="configuration">Configuración de la aplicación.</param>
    internal static IReadOnlyList<string> FindProblems(IConfiguration configuration)
    {
        return RequiredKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .Select(key => $"Falta {AsEnvironmentVariable(key)}.")
            .ToList();
    }

    private static string AsEnvironmentVariable(string key) => key.Replace(":", "__", StringComparison.Ordinal);
}
