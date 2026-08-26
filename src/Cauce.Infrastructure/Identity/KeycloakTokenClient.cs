using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Application.Common.Identity;
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
            ["scope"] = ResolveScope(clientId)
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

        return ToResult(payload);
    }

    /// <inheritdoc />
    public async Task<KeycloakTokenResult> RefreshAsync(string refreshToken, string clientId, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            // Keycloak devuelve 400 invalid_grant cuando el token expiró, se revocó o ya se consumió.
            // Se registra el código concreto para poder distinguir una configuración mal puesta de un
            // token legítimamente vencido, pero al cliente le llega siempre el mismo error.
            var error = await TryReadErrorCodeAsync(response, ct).ConfigureAwait(false);
            _logger.LogWarning(
                "Keycloak rejected a token refresh with status {StatusCode} and error {ErrorCode}.",
                (int)response.StatusCode,
                error ?? "unknown");
            throw new InvalidRefreshTokenException();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Unexpected status {StatusCode} from Keycloak token endpoint during refresh.",
                (int)response.StatusCode);
            throw new InvalidOperationException("No se pudo renovar la sesión con el proveedor de identidad.");
        }

        var payload = await response.Content
            .ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: ct)
            .ConfigureAwait(false);

        if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
        {
            throw new InvalidOperationException("La respuesta del proveedor de identidad no contiene tokens.");
        }

        return ToResult(payload);
    }

    /// <summary>
    /// Determina el scope a solicitar según el cliente OIDC. La app móvil pide además
    /// <c>offline_access</c>: sin él, el refresh token queda atado a la sesión SSO del realm, cuyo
    /// <c>ssoSessionIdleTimeout</c> es de 30 minutos, y la sesión moriría por inactividad mucho antes
    /// de los 30 días previstos para el piloto (DEC-B3-01). El portal web usa sesiones cortas y no lo
    /// necesita.
    /// </summary>
    /// <param name="clientId">Identificador del cliente OIDC.</param>
    /// <returns>El valor del parámetro <c>scope</c>.</returns>
    private static string ResolveScope(string clientId)
    {
        return clientId == OidcClients.Mobile ? "openid offline_access" : "openid";
    }

    private static KeycloakTokenResult ToResult(KeycloakTokenResponse payload)
    {
        return new KeycloakTokenResult(
            payload.AccessToken!,
            payload.RefreshToken ?? string.Empty,
            payload.ExpiresIn,
            payload.RefreshExpiresIn,
            payload.TokenType ?? "Bearer",
            ReadSubject(payload.AccessToken!));
    }

    /// <summary>
    /// Lee el claim <c>sub</c> del access token recién emitido. No valida la firma ni la vigencia: el
    /// token acaba de llegar de Keycloak por un canal de confianza y solo se necesita el identificador
    /// para resolver la cuenta local.
    /// </summary>
    /// <param name="accessToken">Access token en formato JWT compacto.</param>
    /// <returns>El identificador del sujeto, o <see langword="null"/> si no se pudo leer.</returns>
    private static string? ReadSubject(string accessToken)
    {
        try
        {
            var segments = accessToken.Split('.');
            if (segments.Length < 2)
            {
                return null;
            }

            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.TryGetProperty("sub", out var subject)
                ? subject.GetString()
                : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }

    private static async Task<string?> TryReadErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var document = await JsonDocument
                .ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct)
                .ConfigureAwait(false);

            return document.RootElement.TryGetProperty("error", out var error) ? error.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
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
