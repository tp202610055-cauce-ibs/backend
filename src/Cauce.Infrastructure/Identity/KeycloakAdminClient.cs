using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Interfaces.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="IKeycloakAdminClient"/> contra la Admin API de
/// Keycloak. Se autentica con el flujo <c>client_credentials</c> del cliente
/// confidencial del backend y cachea el token de acceso hasta poco antes de su
/// expiración. No registra datos personales.
/// </summary>
public sealed class KeycloakAdminClient : IKeycloakAdminClient
{
    private static readonly TimeSpan TokenRenewalMargin = TimeSpan.FromSeconds(60);

    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakAdminClient> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _cachedAccessToken;
    private DateTime _tokenExpiresAtUtc = DateTime.MinValue;

    /// <summary>
    /// Inicializa el cliente con su <see cref="HttpClient"/> y la configuración de Keycloak.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP tipado.</param>
    /// <param name="options">Opciones de Keycloak.</param>
    /// <param name="logger">Logger de la categoría del cliente.</param>
    public KeycloakAdminClient(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakAdminClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    private string ServerRoot
    {
        get
        {
            var index = _options.Authority.IndexOf("/realms/", StringComparison.Ordinal);
            return index > 0 ? _options.Authority[..index] : _options.Authority.TrimEnd('/');
        }
    }

    private string AdminBaseUrl => $"{ServerRoot}/admin/realms/{_options.Realm}";

    private string TokenUrl => $"{_options.Authority}/protocol/openid-connect/token";

    /// <inheritdoc />
    public async Task<string> CreateUserAsync(
        string email,
        string fullName,
        string roleName,
        bool requireEmailVerification,
        CancellationToken ct = default)
    {
        var (firstName, lastName) = SplitFullName(fullName);
        var payload = new
        {
            username = email,
            email,
            firstName,
            lastName,
            enabled = true,
            emailVerified = !requireEmailVerification,
            requiredActions = requireEmailVerification ? new[] { "VERIFY_EMAIL" } : Array.Empty<string>()
        };

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{AdminBaseUrl}/users")
            {
                Content = JsonContent.Create(payload)
            },
            ct).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw await BuildExceptionAsync(response, "crear el usuario", ct).ConfigureAwait(false);
        }

        var keycloakId = ExtractIdFromLocation(response.Headers.Location)
            ?? throw new KeycloakIntegrationException("Keycloak no devolvió el identificador del usuario creado.");

        await AssignRealmRoleAsync(keycloakId, roleName, ct).ConfigureAwait(false);

        return keycloakId;
    }

    /// <inheritdoc />
    public Task SetTemporaryPasswordAsync(string keycloakUserId, string password, CancellationToken ct = default)
    {
        return ResetPasswordInternalAsync(keycloakUserId, password, temporary: true, ct);
    }

    /// <inheritdoc />
    public Task ResetPasswordAsync(string keycloakUserId, string newPassword, CancellationToken ct = default)
    {
        return ResetPasswordInternalAsync(keycloakUserId, newPassword, temporary: false, ct);
    }

    /// <inheritdoc />
    public async Task SendVerifyEmailAsync(string keycloakUserId, CancellationToken ct = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"{AdminBaseUrl}/users/{keycloakUserId}/send-verify-email"),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, "enviar el correo de verificación", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(string keycloakUserId, CancellationToken ct = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"{AdminBaseUrl}/users/{keycloakUserId}"),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            throw await BuildExceptionAsync(response, "eliminar el usuario", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task DisableUserAsync(string keycloakUserId, CancellationToken ct = default)
    {
        var payload = new { enabled = false };

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"{AdminBaseUrl}/users/{keycloakUserId}")
            {
                Content = JsonContent.Create(payload)
            },
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            throw await BuildExceptionAsync(response, "deshabilitar el usuario", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<KeycloakUserDto?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{AdminBaseUrl}/users?email={encodedEmail}&exact=true"),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, "buscar el usuario por correo", ct).ConfigureAwait(false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var element = document.RootElement[0];
        var id = element.GetProperty("id").GetString() ?? string.Empty;
        var foundEmail = element.TryGetProperty("email", out var emailElement) ? emailElement.GetString() ?? string.Empty : string.Empty;
        var emailVerified = element.TryGetProperty("emailVerified", out var verifiedElement) && verifiedElement.GetBoolean();

        return new KeycloakUserDto(id, foundEmail, emailVerified);
    }

    private async Task ResetPasswordInternalAsync(string keycloakUserId, string password, bool temporary, CancellationToken ct)
    {
        var payload = new { type = "password", value = password, temporary };

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"{AdminBaseUrl}/users/{keycloakUserId}/reset-password")
            {
                Content = JsonContent.Create(payload)
            },
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, "establecer la contraseña", ct).ConfigureAwait(false);
        }
    }

    private async Task AssignRealmRoleAsync(string keycloakUserId, string roleName, CancellationToken ct)
    {
        using var roleResponse = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{AdminBaseUrl}/roles/{Uri.EscapeDataString(roleName)}"),
            ct).ConfigureAwait(false);

        if (!roleResponse.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(roleResponse, "obtener el rol del realm", ct).ConfigureAwait(false);
        }

        await using var stream = await roleResponse.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var roleId = document.RootElement.GetProperty("id").GetString();
        var resolvedRoleName = document.RootElement.GetProperty("name").GetString();
        var rolePayload = new[] { new { id = roleId, name = resolvedRoleName } };

        using var assignResponse = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{AdminBaseUrl}/users/{keycloakUserId}/role-mappings/realm")
            {
                Content = JsonContent.Create(rolePayload)
            },
            ct).ConfigureAwait(false);

        if (!assignResponse.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(assignResponse, "asignar el rol del realm", ct).ConfigureAwait(false);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(forceRefresh: false, ct).ConfigureAwait(false);
        var request = requestFactory();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            response.Dispose();
            token = await GetAccessTokenAsync(forceRefresh: true, ct).ConfigureAwait(false);
            var retry = requestFactory();
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await _httpClient.SendAsync(retry, ct).ConfigureAwait(false);
        }

        return response;
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

            using var response = await _httpClient.PostAsync(TokenUrl, form, ct).ConfigureAwait(false);
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

    private async Task<KeycloakIntegrationException> BuildExceptionAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken ct)
    {
        // Se lee el cuerpo solo para diagnóstico interno; no se propaga al cliente.
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

    private static (string FirstName, string LastName) SplitFullName(string fullName)
    {
        var trimmed = fullName.Trim();
        var separatorIndex = trimmed.IndexOf(' ', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return (trimmed, string.Empty);
        }

        return (trimmed[..separatorIndex], trimmed[(separatorIndex + 1)..].Trim());
    }

    private static string? ExtractIdFromLocation(Uri? location)
    {
        if (location is null)
        {
            return null;
        }

        var segments = location.AbsolutePath.TrimEnd('/').Split('/');
        return segments.Length > 0 ? segments[^1] : null;
    }
}
