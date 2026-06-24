namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Cliente de la Admin API de Keycloak. En esta fase de foundation es un
/// marcador de posición registrado como cliente HTTP tipado; la implementación de
/// los flujos de administración (crear usuario, enviar verificación de correo,
/// cambiar contraseña) se incorpora en el prompt del módulo de Identidad.
/// </summary>
public sealed class KeycloakAdminClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Inicializa el cliente con el <see cref="HttpClient"/> tipado configurado en
    /// la inyección de dependencias.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP configurado para Keycloak.</param>
    public KeycloakAdminClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Dirección base configurada para el cliente HTTP de Keycloak.
    /// </summary>
    public Uri? BaseAddress => _httpClient.BaseAddress;
}
