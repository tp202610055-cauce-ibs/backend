using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.CreateNutritionist;
using Cauce.Application.Identity.UseCases.ResendNutritionistActivationEmail;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Infrastructure.Persistence.Seeders;
using Cauce.Infrastructure.Qa.Manifest;
using MediatR;

namespace Cauce.Infrastructure.Qa.Identity;

/// <summary>
/// Cuenta lista para sembrar sus datos. <see cref="Created"/> indica si nació en esta ejecución.
/// </summary>
public sealed record QaProvisionedAccount(User User, bool Created);

/// <summary>
/// Crea una cuenta del manifiesto en Keycloak y en el backend, o deja una existente en el estado que pide.
/// Las altas nuevas quedan en el contexto sin guardar, salvo las pendientes de activación, que guarda su caso de uso.
/// </summary>
public sealed class QaAccountProvisioner
{
    private readonly IUserRepository _users;
    private readonly IKeycloakAdminClient _keycloak;
    private readonly IQaKeycloakAdmin _qaKeycloak;
    private readonly DevelopmentPatientAccount _patientAccount;
    private readonly ISender _sender;

    /// <summary>
    /// Inicializa el aprovisionador con sus dependencias.
    /// </summary>
    public QaAccountProvisioner(
        IUserRepository users,
        IKeycloakAdminClient keycloak,
        IQaKeycloakAdmin qaKeycloak,
        DevelopmentPatientAccount patientAccount,
        ISender sender)
    {
        _users = users;
        _keycloak = keycloak;
        _qaKeycloak = qaKeycloak;
        _patientAccount = patientAccount;
        _sender = sender;
    }

    /// <summary>
    /// Crea la cuenta si no existe; si existe, ajusta su estado y su contraseña sin tocar sus datos.
    /// </summary>
    /// <param name="account">Cuenta del manifiesto.</param>
    /// <param name="password">Contraseña de su rol; no se usa en las pendientes de activación.</param>
    /// <param name="clock">Reloj de la ejecución; el consentimiento se fecha en el ancla.</param>
    /// <param name="ct">Token de cancelación.</param>
    public async Task<QaProvisionedAccount> EnsureAsync(QaAccount account, string password, QaClock clock, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(clock);
        var email = EmailNormalization.Normalize(account.Email);
        var status = account.Status ?? QaAccountStatus.Active;

        var existing = await _users.FindByEmailAsync(email, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            await RealignAsync(existing, status, password, ct).ConfigureAwait(false);
            return new QaProvisionedAccount(existing, Created: false);
        }

        // Sin cuenta local, una identidad de Keycloak con ese correo es un resto de otra ejecución: se reemplaza.
        var orphan = await _keycloak.FindByEmailAsync(email, ct).ConfigureAwait(false);
        if (orphan is not null)
        {
            await _keycloak.DeleteUserAsync(orphan.Id, ct).ConfigureAwait(false);
        }

        var fullName = QaDefaults.DisplayNameFor(email, account.DisplayName);
        var user = account.Role == QaRole.Patient
            ? await CreatePatientAsync(email, fullName, password, status, clock, ct).ConfigureAwait(false)
            : await CreateNutritionistAsync(email, fullName, password, status, ct).ConfigureAwait(false);

        return new QaProvisionedAccount(user, Created: true);
    }

    /// <summary>
    /// Deja una cuenta existente lista para volver a usarla: contraseña, estado, correo verificado y sin bloqueos.
    /// Una pendiente de activación recibe un enlace nuevo solo si <paramref name="resendActivation"/> lo pide.
    /// </summary>
    public async Task RealignAsync(User user, QaAccountStatus status, string password, CancellationToken ct, bool resendActivation = false)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (status == QaAccountStatus.PendingActivation)
        {
            if (resendActivation && user.Status == UserStatus.PendingActivation)
            {
                await _sender.Send(new ResendNutritionistActivationEmailCommand(user.Id), ct).ConfigureAwait(false);
            }

            return;
        }

        await _keycloak.ResetPasswordAsync(user.KeycloakId, password, ct).ConfigureAwait(false);
        await _qaKeycloak.SetUserReadyAsync(user.KeycloakId, enabled: status == QaAccountStatus.Active, ct).ConfigureAwait(false);
        await _qaKeycloak.ClearBruteForceAsync(user.KeycloakId, ct).ConfigureAwait(false);

        user.Unlock();
        if (!user.EmailVerified)
        {
            user.VerifyEmail();
        }

        if (status == QaAccountStatus.Active && user.Status is UserStatus.Suspended or UserStatus.Inactive)
        {
            user.Reactivate();
        }
        else if (status == QaAccountStatus.Active && user.Status == UserStatus.PendingActivation)
        {
            user.Activate();
        }
        else if (status == QaAccountStatus.Disabled && user.Status != UserStatus.Suspended)
        {
            user.Suspend();
        }
    }

    private async Task<User> CreatePatientAsync(
        string email, string fullName, string password, QaAccountStatus status, QaClock clock, CancellationToken ct)
    {
        var user = await _patientAccount.ProvisionAccountAsync(email, fullName, password, clock.Anchor, ct).ConfigureAwait(false);
        await _qaKeycloak.SetUserReadyAsync(user.KeycloakId, enabled: status == QaAccountStatus.Active, ct).ConfigureAwait(false);
        if (status == QaAccountStatus.Disabled)
        {
            user.Suspend();
        }

        return user;
    }

    private async Task<User> CreateNutritionistAsync(
        string email, string fullName, string password, QaAccountStatus status, CancellationToken ct)
    {
        if (status == QaAccountStatus.PendingActivation)
        {
            // El caso de uso de producción crea la cuenta sin contraseña y pide a Keycloak el enlace de activación.
            var result = await _sender.Send(new CreateNutritionistCommand(email, fullName), ct).ConfigureAwait(false);
            return await _users.FindByIdAsync(result.UserId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("La cuenta pendiente de activación no quedó guardada.");
        }

        var roleId = await _users.GetRoleIdAsync(UserRoles.Nutritionist, ct).ConfigureAwait(false);
        var keycloakId = await _keycloak
            .CreateUserAsync(email, fullName, UserRoles.Nutritionist, requireEmailVerification: false, ct)
            .ConfigureAwait(false);
        await _keycloak.ResetPasswordAsync(keycloakId, password, ct).ConfigureAwait(false);
        await _qaKeycloak.SetUserReadyAsync(keycloakId, enabled: status == QaAccountStatus.Active, ct).ConfigureAwait(false);

        var user = User.CreateNutritionist(Guid.NewGuid(), keycloakId, email, fullName, roleId);
        user.Activate();
        if (status == QaAccountStatus.Disabled)
        {
            user.Suspend();
        }

        await _users.AddAsync(user, ct).ConfigureAwait(false);
        return user;
    }
}
