using System.Collections.Concurrent;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Infrastructure.Qa.Identity;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Doble en memoria de <see cref="IQaKeycloakAdmin"/>: registra el estado pedido para cada cuenta.
/// </summary>
public sealed class FakeQaKeycloakAdmin : IQaKeycloakAdmin
{
    private readonly FakeKeycloakAdminClient _keycloak;

    /// <summary>
    /// Crea el doble sobre el doble de Keycloak, para que el estado pedido se vea al consultar la cuenta.
    /// </summary>
    public FakeQaKeycloakAdmin(FakeKeycloakAdminClient keycloak)
    {
        _keycloak = keycloak;
    }

    /// <summary>
    /// Habilitación pedida por última vez para cada identificador de Keycloak.
    /// </summary>
    public ConcurrentDictionary<string, bool> EnabledByKeycloakId { get; } = new();

    /// <summary>
    /// Identificadores a los que se les limpió el bloqueo por fuerza bruta.
    /// </summary>
    public ConcurrentBag<string> BruteForceCleared { get; } = [];

    /// <summary>
    /// Política que devuelve el doble; por omisión, la del realm de desarrollo.
    /// </summary>
    public string? PasswordPolicy { get; set; } = "length(8) and digits(1) and upperCase(1) and lowerCase(1) and notUsername(undefined)";

    /// <inheritdoc />
    public Task SetUserReadyAsync(string keycloakUserId, bool enabled, CancellationToken ct = default)
    {
        EnabledByKeycloakId[keycloakUserId] = enabled;
        _keycloak.UserStates[keycloakUserId] = new KeycloakUserState(enabled, []);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearBruteForceAsync(string keycloakUserId, CancellationToken ct = default)
    {
        BruteForceCleared.Add(keycloakUserId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetPasswordPolicyAsync(CancellationToken ct = default) => Task.FromResult(PasswordPolicy);
}
