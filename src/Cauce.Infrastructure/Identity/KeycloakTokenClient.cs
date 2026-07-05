using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="IKeycloakTokenClient"/> contra el endpoint de tokens de Keycloak.
/// Hace passthrough del inicio de sesión con <c>grant_type=password</c> (Direct Access Grants) y
/// revoca el refresh token en el cierre de sesión. No registra credenciales ni tokens (acta A3).
/// </summary>
public sealed class KeycloakTokenClient : IKeycloakTokenClient
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakTokenClient> _logger;

    /// <summary>
    /// Inicializa el cliente con su <see cref="HttpClient"/> y la configuración de Keycloak.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP tipado.</param>
    /// <param name="options">Opciones de Keycloak.</param>
    /// <param name="logger">Logger de la categoría del cliente.</param>
    public KeycloakTokenClient(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakTokenClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    private string TokenUrl => $"{_options.Authority}/protocol/openid-connect/token";

    private string LogoutUrl => $"{_options.Authority}/protocol/openid-connect/logout";

    /// <inheritdoc />
    public async Task<KeycloakTokenResult> LoginAsync(string email, string password, string clientId, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = email,
            ["password"] = password,
            ["scope"] = "openid"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            // Mensaje genérico: no se distingue "usuario inexistente" de "contraseña incorrecta".
            throw new InvalidCredentialsException();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Unexpected status {StatusCode} from Keycloak token endpoint during login.",
                (int)response.StatusCode);
            throw new InvalidOperationException("No se pudo completar el inicio de sesión con el proveedor de identidad.");
        }

        var payload = await response.Content
            .ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: ct)
            .ConfigureAwait(false);

        if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
        {
            throw new InvalidOperationException("La respuesta del proveedor de identidad no contiene tokens.");
        }

        return new KeycloakTokenResult(
            payload.AccessToken,
            payload.RefreshToken ?? string.Empty,
            payload.ExpiresIn,
            payload.RefreshExpiresIn,
            payload.TokenType ?? "Bearer");
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string refreshToken, string clientId, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, LogoutUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // El cierre de sesión es best-effort: si Keycloak rechaza la revocación (token ya vencido),
            // se registra pero no se propaga como error al cliente.
            _logger.LogWarning(
                "Keycloak logout returned status {StatusCode}; the refresh token may already be invalid.",
                (int)response.StatusCode);
        }
    }

    private sealed record KeycloakTokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_expires_in")]
        public int RefreshExpiresIn { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string? TokenType { get; init; }
    }
}
