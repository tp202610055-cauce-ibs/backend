using System.Collections.Concurrent;
using Cauce.Application.Common.Interfaces.Identity;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Implementación en memoria de <see cref="IKeycloakAdminClient"/> para pruebas de
/// integración. No contacta a Keycloak: genera identificadores ficticios y registra
/// las operaciones para su inspección.
/// </summary>
public sealed class FakeKeycloakAdminClient : IKeycloakAdminClient
{
    private readonly ConcurrentDictionary<string, string> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Identificadores de Keycloak para los que se solicitó el correo de verificación.
    /// </summary>
    public List<string> VerifyEmailsSent { get; } = [];

    /// <summary>
    /// Identificadores de Keycloak eliminados (compensaciones).
    /// </summary>
    public List<string> DeletedUsers { get; } = [];

    /// <summary>
    /// Identificadores de Keycloak deshabilitados (anonimización de cuentas, US26).
    /// </summary>
    public List<string> DisabledUsers { get; } = [];

    /// <inheritdoc />
    public Task<string> CreateUserAsync(string email, string fullName, string roleName, bool requireEmailVerification, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString();
        _usersByEmail[email] = id;
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public Task SetTemporaryPasswordAsync(string keycloakUserId, string password, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task ResetPasswordAsync(string keycloakUserId, string newPassword, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task SendVerifyEmailAsync(string keycloakUserId, CancellationToken ct = default)
    {
        VerifyEmailsSent.Add(keycloakUserId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteUserAsync(string keycloakUserId, CancellationToken ct = default)
    {
        DeletedUsers.Add(keycloakUserId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisableUserAsync(string keycloakUserId, CancellationToken ct = default)
    {
        DisabledUsers.Add(keycloakUserId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<KeycloakUserDto?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        return Task.FromResult(_usersByEmail.TryGetValue(email, out var id)
            ? new KeycloakUserDto(id, email, false)
            : null);
    }
}
