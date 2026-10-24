using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces.Identity;
using Microsoft.AspNetCore.WebUtilities;
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
    private const string UpdatePasswordAction = "UPDATE_PASSWORD";

    private readonly KeycloakAdminTransport _transport;
    private readonly KeycloakOptions _options;
    private readonly IClientUrlProvider _clientUrlProvider;
    private readonly ILogger<KeycloakAdminClient> _logger;

    /// <summary>
    /// Inicializa el cliente con su <see cref="HttpClient"/> y la configuración de Keycloak.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP tipado.</param>
    /// <param name="options">Opciones de Keycloak.</param>
    /// <param name="clientUrlProvider">Proveedor de las URL de los clientes, para el retorno al portal.</param>
    /// <param name="logger">Logger de la categoría del cliente.</param>
    public KeycloakAdminClient(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        IClientUrlProvider clientUrlProvider,
        ILogger<KeycloakAdminClient> logger)
    {
        _options = options.Value;
        _transport = new KeycloakAdminTransport(httpClient, _options, logger);
        _clientUrlProvider = clientUrlProvider;
        _logger = logger;
    }

    private string AdminBaseUrl => _transport.AdminBaseUrl;

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

        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{AdminBaseUrl}/users")
            {
                Content = JsonContent.Create(payload)
            },
            ct).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw await _transport.BuildExceptionAsync(response, "crear el usuario", ct).ConfigureAwait(false);
        }

        var keycloakId = ExtractIdFromLocation(response.Headers.Location)
            ?? throw new KeycloakIntegrationException("Keycloak no devolvió el identificador del usuario creado.");

        try
        {
            await AssignRealmRoleAsync(keycloakId, roleName, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // El usuario ya existe en Keycloak y el llamador todavía no conoce su identificador: sin el rol
            // quedaría huérfano, sin cuenta local y bloqueando una nueva alta con el mismo correo (acta A70).
            await KeycloakCompensation.TryDeleteUserAsync(this, keycloakId, _logger).ConfigureAwait(false);
            throw;
        }

        return keycloakId;
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(string keycloakUserId, string newPassword, CancellationToken ct = default)
    {
        var payload = new { type = "password", value = newPassword, temporary = false };

        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"{AdminBaseUrl}/users/{keycloakUserId}/reset-password")
            {
                Content = JsonContent.Create(payload)
            },
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await _transport.BuildExceptionAsync(response, "establecer la contraseña", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task SendVerifyEmailAsync(string keycloakUserId, CancellationToken ct = default)
    {
        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"{AdminBaseUrl}/users/{keycloakUserId}/send-verify-email"),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await _transport.BuildExceptionAsync(response, "enviar el correo de verificación", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task SendUpdatePasswordEmailAsync(string keycloakUserId, CancellationToken ct = default)
    {
        // Sin el parámetro lifespan: rige actionTokenGeneratedByAdminLifespan del realm, para que la vigencia
        // del enlace se configure en un solo lugar. Con client_id y redirect_uri, la última pantalla ofrece
        // volver al login del portal (acta A68); Keycloak rechaza la petición si la URI no está entre las
        // redirect URIs del cliente del portal.
        var url = $"{AdminBaseUrl}/users/{keycloakUserId}/execute-actions-email";
        var portalLoginUrl = _clientUrlProvider.BuildPortalLoginUrl();
        if (portalLoginUrl is not null)
        {
            url = QueryHelpers.AddQueryString(url, new Dictionary<string, string?>
            {
                ["client_id"] = OidcClients.WebPortal,
                ["redirect_uri"] = portalLoginUrl
            });
        }

        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(new[] { UpdatePasswordAction })
            },
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await _transport.BuildExceptionAsync(response, "enviar el enlace para definir la contraseña", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(string keycloakUserId, CancellationToken ct = default)
    {
        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"{AdminBaseUrl}/users/{keycloakUserId}"),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            throw await _transport.BuildExceptionAsync(response, "eliminar el usuario", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task DisableUserAsync(string keycloakUserId, CancellationToken ct = default)
    {
        var payload = new { enabled = false };

        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"{AdminBaseUrl}/users/{keycloakUserId}")
            {
                Content = JsonContent.Create(payload)
            },
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            throw await _transport.BuildExceptionAsync(response, "deshabilitar el usuario", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<KeycloakUserDto?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{AdminBaseUrl}/users?email={encodedEmail}&exact=true"),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await _transport.BuildExceptionAsync(response, "buscar el usuario por correo", ct).ConfigureAwait(false);
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

    /// <inheritdoc />
    public async Task<bool> GetUserEmailVerifiedAsync(string keycloakUserId, CancellationToken ct = default)
    {
        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{AdminBaseUrl}/users/{keycloakUserId}"),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Incluye el 404 del usuario desincronizado: no se normaliza a false, porque "no lo pude
            // averiguar" y "no está verificado" son cosas distintas y el llamador debe poder
            // distinguirlas para no degradar el estado local (acta A39).
            throw await _transport.BuildExceptionAsync(response, "consultar la verificación del correo", ct).ConfigureAwait(false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        return document.RootElement.TryGetProperty("emailVerified", out var verifiedElement)
            && verifiedElement.GetBoolean();
    }

    /// <inheritdoc />
    public async Task<KeycloakUserState> GetUserStateAsync(string keycloakUserId, CancellationToken ct = default)
    {
        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{AdminBaseUrl}/users/{keycloakUserId}"),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await _transport.BuildExceptionAsync(response, "consultar el estado del usuario", ct).ConfigureAwait(false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = document.RootElement;

        // Keycloak omite "enabled" solo en representaciones parciales; ante la duda se asume habilitado,
        // porque este dato explica un rechazo y no decide ningún acceso.
        var enabled = !root.TryGetProperty("enabled", out var enabledElement) || enabledElement.GetBoolean();
        var requiredActions = root.TryGetProperty("requiredActions", out var actionsElement)
            && actionsElement.ValueKind == JsonValueKind.Array
                ? actionsElement.EnumerateArray()
                    .Select(action => action.GetString())
                    .OfType<string>()
                    .ToList()
                : [];

        return new KeycloakUserState(enabled, requiredActions);
    }

    /// <inheritdoc />
    public async Task<BruteForceStatus?> GetBruteForceStatusAsync(string keycloakUserId, CancellationToken ct = default)
    {
        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(
                HttpMethod.Get,
                $"{AdminBaseUrl}/attack-detection/brute-force/users/{keycloakUserId}"),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await _transport.BuildExceptionAsync(response, "consultar el estado de fuerza bruta", ct).ConfigureAwait(false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = document.RootElement;

        var numFailures = root.TryGetProperty("numFailures", out var failures) ? failures.GetInt32() : 0;
        if (numFailures == 0)
        {
            // Keycloak responde 200 con el contador en cero para un usuario que nunca falló; se
            // normaliza a null para que el llamador no tenga que distinguir ese caso.
            return null;
        }

        var disabled = root.TryGetProperty("disabled", out var disabledElement) && disabledElement.GetBoolean();
        var lastFailure = root.TryGetProperty("lastFailure", out var lastFailureElement)
            && lastFailureElement.TryGetInt64(out var lastFailureValue)
            && lastFailureValue > 0
                ? lastFailureValue
                : (long?)null;
        var lastIpFailure = root.TryGetProperty("lastIPFailure", out var ipElement) ? ipElement.GetString() : null;

        // Keycloak reporta el momento del último fallo, no el del desbloqueo. El realm suma
        // waitIncrementSeconds a partir de ahí; si no reporta la marca, se toma el instante actual
        // como base para no devolver una ventana vacía.
        var lockedFrom = lastFailure is not null
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastFailure.Value).UtcDateTime
            : DateTime.UtcNow;
        var lockedUntil = lockedFrom.AddSeconds(_options.WaitIncrementSeconds);

        return new BruteForceStatus(disabled, numFailures, lastFailure, lastIpFailure, lockedUntil);
    }

    private async Task AssignRealmRoleAsync(string keycloakUserId, string roleName, CancellationToken ct)
    {
        using var roleResponse = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{AdminBaseUrl}/roles/{Uri.EscapeDataString(roleName)}"),
            ct).ConfigureAwait(false);

        if (!roleResponse.IsSuccessStatusCode)
        {
            throw await _transport.BuildExceptionAsync(roleResponse, "obtener el rol del realm", ct).ConfigureAwait(false);
        }

        await using var stream = await roleResponse.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var roleId = document.RootElement.GetProperty("id").GetString();
        var resolvedRoleName = document.RootElement.GetProperty("name").GetString();
        var rolePayload = new[] { new { id = roleId, name = resolvedRoleName } };

        using var assignResponse = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{AdminBaseUrl}/users/{keycloakUserId}/role-mappings/realm")
            {
                Content = JsonContent.Create(rolePayload)
            },
            ct).ConfigureAwait(false);

        if (!assignResponse.IsSuccessStatusCode)
        {
            throw await _transport.BuildExceptionAsync(assignResponse, "asignar el rol del realm", ct).ConfigureAwait(false);
        }
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
