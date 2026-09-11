using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.Login;

/// <summary>
/// Handler del inicio de sesión. Delega la validación de credenciales a Keycloak y, si es exitosa,
/// actualiza la fecha del último acceso del usuario local y activa al nutricionista que se autentica por
/// primera vez. El evento LOGIN se audita en el middleware; este handler no lo registra.
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IKeycloakTokenClient _tokenClient;
    private readonly IKeycloakAdminClient _adminClient;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistActivationService _activationService;
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
        IUnitOfWork unitOfWork,
        ILogger<LoginCommandHandler> logger)
    {
        _tokenClient = tokenClient;
        _adminClient = adminClient;
        _userRepository = userRepository;
        _activationService = activationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
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
            // necesita para mostrar el tiempo de espera.
            var lockedUntil = await TryResolveLockoutAsync(request.Email, cancellationToken).ConfigureAwait(false);
            if (lockedUntil is not null)
            {
                throw new AccountLockedException(lockedUntil.Value);
            }

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
            throw new UserLocalMissingException();
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
    /// Determina si unas credenciales rechazadas corresponden a una cuenta bloqueada por intentos
    /// fallidos, y hasta cuándo lo está.
    /// </summary>
    /// <param name="email">Correo con el que se intentó iniciar sesión.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// El momento de desbloqueo si la cuenta está bloqueada; <see langword="null"/> si no lo está, si
    /// no existe cuenta local, o si no se pudo determinar.
    /// </returns>
    private async Task<DateTime?> TryResolveLockoutAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            // Sin cuenta local no hay nada que consultar, y responder distinto revelaría qué correos
            // están registrados.
            return null;
        }

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
}
