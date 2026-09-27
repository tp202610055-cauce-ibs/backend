using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.Services;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.Login;

/// <summary>
/// Handler del inicio de sesión. Delega la validación de credenciales a Keycloak y, si es exitosa,
/// actualiza la fecha del último acceso del usuario local y activa al nutricionista que se autentica por
/// primera vez. El evento LOGIN se audita en el middleware; este handler no lo registra, pero deja en el
/// <see cref="IAuthenticationAttemptContext"/> la causa interna de cada rechazo (acta A68).
/// </summary>
/// <remarks>
/// Tiene más dependencias que las que sugiere la convención porque orquesta el inicio de sesión
/// completo: tokens, diagnóstico del rechazo, cuenta local, activación y contexto de auditoría. Separarlo
/// repartiría una sola decisión en varias clases.
/// </remarks>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IKeycloakTokenClient _tokenClient;
    private readonly IKeycloakAdminClient _adminClient;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistActivationService _activationService;
    private readonly IAuthenticationAttemptContext _attemptContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LoginCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public LoginCommandHandler(
        IKeycloakTokenClient tokenClient,
        IKeycloakAdminClient adminClient,
        IUserRepository userRepository,
        INutritionistActivationService activationService,
        IAuthenticationAttemptContext attemptContext,
        IUnitOfWork unitOfWork,
        ILogger<LoginCommandHandler> logger)
    {
        _tokenClient = tokenClient;
        _adminClient = adminClient;
        _userRepository = userRepository;
        _activationService = activationService;
        _attemptContext = attemptContext;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.ClientId, OidcClients.ForChannel(request.Channel), StringComparison.Ordinal))
        {
            // Sin esta regla, /auth/login con el cliente del portal devolvería el refresh token en el cuerpo
            // y saltearía el filtro de rol del portal (acta A68).
            _attemptContext.RecordFailure(AuthFailureCauses.UnsupportedClient);
            throw new UnsupportedOidcClientException();
        }

        KeycloakTokenResult token;
        try
        {
            token = await _tokenClient
                .LoginAsync(request.Email, request.Password, request.ClientId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidCredentialsException)
        {
            // Keycloak colapsa "contraseña incorrecta" y "cuenta bloqueada" en el mismo 401. Solo
            // consultando la detección de fuerza bruta se pueden distinguir, que es lo que US05 CA02
            // necesita para mostrar el tiempo de espera. El resto de las causas va solo a la auditoría.
            var (cause, lockedUntil) = await ExplainRejectionAsync(request.Email, cancellationToken)
                .ConfigureAwait(false);
            _attemptContext.RecordFailure(cause);
            if (lockedUntil is not null)
            {
                throw new AccountLockedException(lockedUntil.Value);
            }

            throw;
        }
        catch (IdentityProviderMisconfiguredException)
        {
            _attemptContext.RecordFailure(AuthFailureCauses.ClientMisconfigured);
            throw;
        }

        var user = await _userRepository.FindByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            // Keycloak autenticó pero la cuenta local no existe: el aprovisionamiento quedó a medias.
            // Antes esto pasaba en silencio y se devolvían tokens de un usuario que el backend no
            // conoce, así que el cliente arrancaba sesión contra una identidad fantasma.
            _logger.LogCritical(
                "Authenticated subject without a local user account for {MaskedEmail}.",
                AuditMask.Email(request.Email));
            _attemptContext.RecordFailure(AuthFailureCauses.LocalAccountMissing);
            throw new UserLocalMissingException();
        }

        if (request.Channel == LoginChannel.Portal)
        {
            await EnsurePortalAccessAsync(user, token, request.ClientId, cancellationToken).ConfigureAwait(false);
        }

        // Antes del SaveChanges para que una sola transacción persista la marca de acceso y el estado
        // de verificación sincronizado.
        await TrySyncEmailVerifiedAsync(user, cancellationToken).ConfigureAwait(false);

        // En esta petición no hay token que el behavior de activación pueda leer: la identidad aparece
        // recién aquí, cuando Keycloak responde. Por eso el login invoca la regla directamente (acta A51).
        await _activationService
            .ActivateIfPendingAsync(user, NutritionistActivationTrigger.Login, cancellationToken)
            .ConfigureAwait(false);

        user.RegisterSuccessfulLogin(DateTime.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Login succeeded for user {UserId}.", user.Id);

        var roleName = await _userRepository
            .GetRoleNameAsync(user.RoleId, cancellationToken)
            .ConfigureAwait(false);

        return new LoginResult(
            token.AccessToken,
            token.RefreshToken,
            token.ExpiresIn,
            token.RefreshExpiresIn,
            token.TokenType,
            new AuthenticatedUser(
                user.Id,
                user.KeycloakId,
                user.Email,
                roleName,
                user.FullName,
                user.EmailVerified,
                user.IsInActivePilot));
    }

    /// <summary>
    /// Aplica las reglas de acceso del portal a una cuenta que Keycloak ya autenticó. Si la cuenta no
    /// puede usar el portal, revoca la sesión que Keycloak acaba de abrir y responde con el mismo 401 que
    /// una contraseña incorrecta: el portal no revela si la cuenta existe ni por qué no entra (acta A68).
    /// </summary>
    /// <param name="user">Cuenta local autenticada.</param>
    /// <param name="token">Tokens recién emitidos por Keycloak.</param>
    /// <param name="clientId">Cliente OIDC del portal.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    /// <exception cref="InvalidCredentialsException">Si la cuenta no puede usar el portal.</exception>
    private async Task EnsurePortalAccessAsync(
        User user,
        KeycloakTokenResult token,
        string clientId,
        CancellationToken cancellationToken)
    {
        var cause = await PortalAccessRules
            .FindRejectionCauseAsync(user, _userRepository, cancellationToken)
            .ConfigureAwait(false);
        if (cause is null)
        {
            return;
        }

        _attemptContext.RecordFailure(cause);
        _logger.LogWarning("Portal login rejected for user {UserId} ({Cause}).", user.Id, cause);

        // Keycloak ya abrió una sesión con esas credenciales: se revoca para no dejarla viva sin dueño. Si la
        // revocación falla, el rechazo se mantiene igual: la sesión caduca sola a los 30 minutos sin uso.
        try
        {
            await _tokenClient
                .LogoutAsync(token.RefreshToken, clientId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not revoke the rejected portal session of user {UserId}.", user.Id);
        }

        throw new InvalidCredentialsException();
    }

    /// <summary>
    /// Sincroniza el estado de verificación del correo desde Keycloak, que es su fuente de verdad: el
    /// enlace de confirmación lo emite y lo procesa el realm sin pasar por el backend, así que la copia
    /// local se desactualiza en cuanto el paciente verifica (acta A39).
    /// </summary>
    /// <remarks>
    /// La sincronización es <b>unidireccional</b>: solo promueve un correo a verificado. Si Keycloak
    /// reporta como no verificado un correo que localmente sí lo está, el valor local no se revierte y
    /// se registra una advertencia. Des-verificar es un cambio de estado sensible que exige un flujo
    /// explícito y auditado, no un efecto colateral de un inicio de sesión.
    /// <para>
    /// No persiste: la escritura queda en el <c>ChangeTracker</c> y la confirma el
    /// <c>SaveChangesAsync</c> del llamador, en la misma transacción que la marca de último acceso.
    /// </para>
    /// </remarks>
    /// <param name="user">Usuario local ya autenticado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    private async Task TrySyncEmailVerifiedAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var verifiedInKeycloak = await _adminClient
                .GetUserEmailVerifiedAsync(user.KeycloakId, cancellationToken)
                .ConfigureAwait(false);

            if (verifiedInKeycloak == user.EmailVerified)
            {
                return;
            }

            if (!verifiedInKeycloak)
            {
                _logger.LogWarning(
                    "Keycloak reports an unverified email for user {UserId} that is verified locally; keeping the local value.",
                    user.Id);
                return;
            }

            user.VerifyEmail();
            _logger.LogInformation("Synced emailVerified from Keycloak for user {UserId}.", user.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Un fallo de la Admin API no debe convertir un login válido en un error: se degrada al
            // valor local y la sesión continúa (acta A39, decisión D2).
            _logger.LogWarning(
                exception,
                "Could not sync emailVerified from Keycloak for user {UserId}; continuing with the local value.",
                user.Id);
        }
    }

    /// <summary>
    /// Explica por qué Keycloak rechazó unas credenciales. Si la cuenta está bloqueada por intentos
    /// fallidos, devuelve además hasta cuándo, porque eso sí se informa al cliente (US05 CA02). El resto de
    /// las causas sale del estado local y de la Admin API, nunca del texto de error de Keycloak, y solo
    /// llega a la auditoría (acta A68).
    /// </summary>
    /// <param name="email">Correo con el que se intentó iniciar sesión.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// La causa interna del rechazo y, si la cuenta está bloqueada, el momento de desbloqueo.
    /// </returns>
    private async Task<(string Cause, DateTime? LockedUntil)> ExplainRejectionAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            // Sin cuenta local no hay nada que consultar, y responder distinto revelaría qué correos
            // están registrados.
            return (AuthFailureCauses.UnknownAccount, null);
        }

        var lockedUntil = await TryResolveLockoutAsync(user, cancellationToken).ConfigureAwait(false);
        if (lockedUntil is not null)
        {
            return (AuthFailureCauses.AccountLocked, lockedUntil);
        }

        return (await DescribeAccountStateAsync(user, cancellationToken).ConfigureAwait(false), null);
    }

    /// <summary>
    /// Determina si la cuenta está bloqueada por intentos fallidos, y hasta cuándo.
    /// </summary>
    /// <param name="user">Cuenta local cuyas credenciales se rechazaron.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// El momento de desbloqueo si la cuenta está bloqueada; <see langword="null"/> si no lo está o si no
    /// se pudo determinar.
    /// </returns>
    private async Task<DateTime?> TryResolveLockoutAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _adminClient
                .GetBruteForceStatusAsync(user.KeycloakId, cancellationToken)
                .ConfigureAwait(false);

            return status is { Disabled: true } ? status.LockedUntil : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Si la Admin API falla o no responde, se degrada al comportamiento anterior: el cliente
            // recibe el 401 genérico. Un fallo de diagnóstico no debe convertir un login rechazado en
            // un error del servidor, ni filtrar al cliente que la consulta falló.
            _logger.LogWarning(
                exception,
                "Could not read the brute force status for user {UserId}; falling back to invalid_credentials.",
                user.Id);
            return null;
        }
    }

    /// <summary>
    /// Describe, para la auditoría, el estado de una cuenta cuyas credenciales Keycloak rechazó. Va de lo
    /// más determinante a lo menos: cuenta deshabilitada, estado local que impide entrar, acción pendiente
    /// en Keycloak y, si nada de eso aplica, contraseña incorrecta.
    /// </summary>
    /// <param name="user">Cuenta local cuyas credenciales se rechazaron.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Una de las causas de <see cref="AuthFailureCauses"/>.</returns>
    private async Task<string> DescribeAccountStateAsync(User user, CancellationToken cancellationToken)
    {
        KeycloakUserState state;
        try
        {
            state = await _adminClient.GetUserStateAsync(user.KeycloakId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Could not read the Keycloak state of user {UserId}; the rejection cause stays undetermined.",
                user.Id);
            return LocalStateCause(user) ?? AuthFailureCauses.Undetermined;
        }

        if (!state.Enabled)
        {
            return AuthFailureCauses.AccountDisabled;
        }

        if (LocalStateCause(user) is { } localCause)
        {
            return localCause;
        }

        return state.RequiredActions.Count > 0
            ? AuthFailureCauses.RequiredActionPending
            : AuthFailureCauses.WrongPassword;
    }

    private static string? LocalStateCause(User user)
    {
        return user.Status switch
        {
            UserStatus.PendingActivation => AuthFailureCauses.PendingActivation,
            UserStatus.Suspended => AuthFailureCauses.AccountSuspended,
            UserStatus.Inactive => AuthFailureCauses.AccountInactive,
            _ => null
        };
    }
}
