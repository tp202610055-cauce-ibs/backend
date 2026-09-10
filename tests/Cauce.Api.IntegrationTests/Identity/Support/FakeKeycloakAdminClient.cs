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

    /// <summary>
    /// Estado de fuerza bruta que devuelve el doble, indexado por identificador de Keycloak. Si un
    /// usuario no está aquí, se considera que nunca falló.
    /// </summary>
    public ConcurrentDictionary<string, BruteForceStatus> BruteForceStatuses { get; } = new();

    /// <summary>
    /// Excepción que lanza <see cref="GetBruteForceStatusAsync"/>, para ejercitar la degradación
    /// cuando la Admin API no responde.
    /// </summary>
    public Exception? BruteForceFailure { get; set; }

    /// <summary>
    /// Marca a un usuario como bloqueado por fuerza bruta hasta el momento indicado.
    /// </summary>
    /// <param name="keycloakUserId">Identificador del usuario en Keycloak.</param>
    /// <param name="lockedUntil">Momento UTC de desbloqueo.</param>
    public void LockUser(string keycloakUserId, DateTime lockedUntil)
    {
        BruteForceStatuses[keycloakUserId] = new BruteForceStatus(
            Disabled: true,
            NumFailures: 5,
            LastFailure: new DateTimeOffset(lockedUntil.AddSeconds(-60), TimeSpan.Zero).ToUnixTimeMilliseconds(),
            LastIPFailure: "127.0.0.1",
            LockedUntil: lockedUntil);
    }

    /// <summary>
    /// Estado de verificación del correo que devuelve el doble, indexado por identificador de
    /// Keycloak. Si un usuario no está aquí, se considera no verificado.
    /// </summary>
    public ConcurrentDictionary<string, bool> EmailVerifiedByKeycloakId { get; } = new();

    /// <summary>
    /// Excepción que lanza <see cref="GetUserEmailVerifiedAsync"/>, para ejercitar la degradación del
    /// login cuando la Admin API no responde (acta A39).
    /// </summary>
    public Exception? EmailVerifiedFailure { get; set; }

    /// <summary>
    /// Marca el correo de un usuario como verificado en Keycloak, sin tocar la copia local.
    /// </summary>
    /// <param name="keycloakUserId">Identificador del usuario en Keycloak.</param>
    public void MarkEmailVerifiedInKeycloak(string keycloakUserId)
    {
        EmailVerifiedByKeycloakId[keycloakUserId] = true;
    }

    /// <inheritdoc />
    public Task<BruteForceStatus?> GetBruteForceStatusAsync(string keycloakUserId, CancellationToken ct = default)
    {
        if (BruteForceFailure is not null)
        {
            throw BruteForceFailure;
        }

        return Task.FromResult(BruteForceStatuses.TryGetValue(keycloakUserId, out var status) ? status : null);
    }

    /// <inheritdoc />
    public Task<bool> GetUserEmailVerifiedAsync(string keycloakUserId, CancellationToken ct = default)
    {
        if (EmailVerifiedFailure is not null)
        {
            throw EmailVerifiedFailure;
        }

        return Task.FromResult(
            EmailVerifiedByKeycloakId.TryGetValue(keycloakUserId, out var verified) && verified);
    }

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
