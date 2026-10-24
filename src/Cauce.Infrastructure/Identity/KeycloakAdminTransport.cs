using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Cauce.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Transporte autenticado hacia la Admin API de Keycloak: obtiene y cachea el token <c>client_credentials</c>,
/// reintenta una vez ante 401 o 403 y traduce las fallas de red a <see cref="KeycloakIntegrationException"/>.
/// </summary>
internal sealed class KeycloakAdminTransport
{
    private static readonly TimeSpan TokenRenewalMargin = TimeSpan.FromSeconds(60);

    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _cachedAccessToken;
    private DateTime _tokenExpiresAtUtc = DateTime.MinValue;

    /// <summary>
    /// Inicializa el transporte con el cliente HTTP, la configuración de Keycloak y el logger del llamador.
    /// </summary>
    public KeycloakAdminTransport(HttpClient httpClient, KeycloakOptions options, ILogger logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// URL base de la Admin API del realm configurado.
    /// </summary>
    public string AdminBaseUrl => $"{_options.ServerRoot}/admin/realms/{_options.Realm}";

    private string TokenUrl => $"{_options.BackchannelAuthority}/protocol/openid-connect/token";

    /// <summary>
    /// Envía una petición autenticada. La fábrica se invoca de nuevo si hay que reintentar con un token renovado.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(forceRefresh: false, ct).ConfigureAwait(false);
        var request = requestFactory();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await SendOverNetworkAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            response.Dispose();
            token = await GetAccessTokenAsync(forceRefresh: true, ct).ConfigureAwait(false);
            var retry = requestFactory();
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await SendOverNetworkAsync(retry, ct).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>
    /// Arma la excepción de una respuesta fallida. El cuerpo solo se mide para el log; no se propaga al cliente.
    /// </summary>
    public async Task<KeycloakIntegrationException> BuildExceptionAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _logger.LogError(
            "Keycloak operation '{Operation}' failed with status {StatusCode}. Body length: {BodyLength}.",
            operation,
            (int)response.StatusCode,
            body.Length);

        return new KeycloakIntegrationException(
            $"Error al {operation} en el proveedor de identidad.",
            (int)response.StatusCode);
    }

    /// <summary>
    /// Envía una petición y traduce el timeout y los errores de conexión a <see cref="KeycloakIntegrationException"/>,
    /// que el API responde como 502. Una cancelación pedida por el llamador se propaga tal cual.
    /// </summary>
    private async Task<HttpResponseMessage> SendOverNetworkAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Keycloak could not be reached ({Method} request).", request.Method);
            throw new KeycloakIntegrationException("No se pudo conectar con el proveedor de identidad.", exception);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            // La cancelación no vino del llamador: es el timeout del HttpClient.
            _logger.LogError(exception, "Keycloak did not respond in time ({Method} request).", request.Method);
            throw new KeycloakIntegrationException("El proveedor de identidad no respondió a tiempo.", exception);
        }
    }

    private async Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && _cachedAccessToken is not null && DateTime.UtcNow < _tokenExpiresAtUtc)
        {
            return _cachedAccessToken;
        }

        await _tokenLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _cachedAccessToken is not null && DateTime.UtcNow < _tokenExpiresAtUtc)
            {
                return _cachedAccessToken;
            }

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = form };
            using var response = await SendOverNetworkAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw await BuildExceptionAsync(response, "obtener el token de administración", ct).ConfigureAwait(false);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            var accessToken = document.RootElement.GetProperty("access_token").GetString()
                ?? throw new KeycloakIntegrationException("Keycloak no devolvió un token de acceso.");
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiresElement)
                ? expiresElement.GetInt32()
                : 60;

            _cachedAccessToken = accessToken;
            _tokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn) - TokenRenewalMargin;

            return accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
