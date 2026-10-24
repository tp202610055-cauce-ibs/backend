using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Qa.Identity;

/// <summary>
/// Operaciones de la Admin API de Keycloak que solo usan los comandos de QA para dejar cuentas en un estado conocido.
/// </summary>
public interface IQaKeycloakAdmin
{
    /// <summary>
    /// Deja la cuenta con el correo verificado y sin acciones pendientes, habilitada o deshabilitada.
    /// </summary>
    Task SetUserReadyAsync(string keycloakUserId, bool enabled, CancellationToken ct = default);

    /// <summary>
    /// Borra el registro de intentos fallidos de la cuenta, con lo que se levanta un bloqueo por fuerza bruta.
    /// </summary>
    Task ClearBruteForceAsync(string keycloakUserId, CancellationToken ct = default);

    /// <summary>
    /// Política de contraseñas del realm, o <see langword="null"/> si el realm no define ninguna.
    /// </summary>
    Task<string?> GetPasswordPolicyAsync(CancellationToken ct = default);
}

/// <summary>
/// Implementación de <see cref="IQaKeycloakAdmin"/> sobre el transporte autenticado de la Admin API.
/// </summary>
public sealed class QaKeycloakAdmin : IQaKeycloakAdmin
{
    private readonly KeycloakAdminTransport _transport;

    /// <summary>
    /// Inicializa el cliente con su <see cref="HttpClient"/> y la configuración de Keycloak.
    /// </summary>
    public QaKeycloakAdmin(HttpClient httpClient, IOptions<KeycloakOptions> options, ILogger<QaKeycloakAdmin> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _transport = new KeycloakAdminTransport(httpClient, options.Value, logger);
    }

    /// <inheritdoc />
    public async Task SetUserReadyAsync(string keycloakUserId, bool enabled, CancellationToken ct = default)
    {
        var payload = new { enabled, emailVerified = true, requiredActions = Array.Empty<string>() };

        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"{_transport.AdminBaseUrl}/users/{keycloakUserId}")
            {
                Content = JsonContent.Create(payload)
            },
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await _transport.BuildExceptionAsync(response, "preparar la cuenta de QA", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task ClearBruteForceAsync(string keycloakUserId, CancellationToken ct = default)
    {
        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(
                HttpMethod.Delete,
                $"{_transport.AdminBaseUrl}/attack-detection/brute-force/users/{keycloakUserId}"),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            throw await _transport.BuildExceptionAsync(response, "limpiar el bloqueo por fuerza bruta", ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetPasswordPolicyAsync(CancellationToken ct = default)
    {
        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, _transport.AdminBaseUrl),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await _transport.BuildExceptionAsync(response, "leer la política de contraseñas del realm", ct).ConfigureAwait(false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        return document.RootElement.TryGetProperty("passwordPolicy", out var policy) && policy.ValueKind == JsonValueKind.String
            ? policy.GetString()
            : null;
    }
}
