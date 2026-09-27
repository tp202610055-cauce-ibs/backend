using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.Services;
using Cauce.Application.Identity.UseCases.Login;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.RefreshToken;

/// <summary>
/// Handler de la renovación de sesión. Delega el intercambio del token a Keycloak, resuelve la cuenta
/// local por el <c>sub</c> del token recién emitido y audita el acceso.
/// </summary>
/// <remarks>
/// La auditoría se escribe aquí y no en el <c>AuditingMiddleware</c>: el middleware resuelve el actor
/// del login leyendo el correo del cuerpo, y una petición de renovación no lo lleva. Además el
/// endpoint es anónimo, así que el resolutor de actor por principal tampoco daría resultado. El
/// handler sí conoce la cuenta, y la pasa explícitamente. Cada fila lleva el canal y, si hubo rechazo,
/// su causa interna (acta A68).
/// </remarks>
public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResult>
{
    private readonly IKeycloakTokenClient _tokenClient;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public RefreshTokenCommandHandler(
        IKeycloakTokenClient tokenClient,
        IUserRepository userRepository,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _tokenClient = tokenClient;
        _userRepository = userRepository;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.ClientId, OidcClients.ForChannel(request.Channel), StringComparison.Ordinal))
        {
            await AuditFailureAsync(request, AuthFailureCauses.UnsupportedClient, null, cancellationToken)
                .ConfigureAwait(false);
            throw new UnsupportedOidcClientException();
        }

        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            // Solo llega vacío desde el portal: el validador lo exige en el móvil. Es el caso normal de un
            // portal que arranca sin sesión, pero se audita igual que cualquier intento rechazado.
            await AuditFailureAsync(request, AuthFailureCauses.MissingRefreshCookie, null, cancellationToken)
                .ConfigureAwait(false);
            throw new InvalidRefreshTokenException();
        }

        KeycloakTokenResult token;
        try
        {
            token = await _tokenClient
                .RefreshAsync(request.RefreshToken, request.ClientId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidRefreshTokenException)
        {
            // Sin actor: el token rechazado no permite resolver la cuenta con garantías. La fila
            // conserva IP y user-agent, que es lo que da valor forense al registro.
            await AuditFailureAsync(request, AuthFailureCauses.InvalidRefreshToken, null, cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
        catch (IdentityProviderMisconfiguredException)
        {
            await AuditFailureAsync(request, AuthFailureCauses.ClientMisconfigured, null, cancellationToken)
                .ConfigureAwait(false);
            throw;
        }

        if (string.IsNullOrEmpty(token.Subject))
        {
            _logger.LogCritical("Keycloak issued a refreshed token without a sub claim.");
            throw new UserLocalMissingException();
        }

        var user = await _userRepository
            .FindByKeycloakIdAsync(token.Subject, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            _logger.LogCritical(
                "Refreshed a session for a Keycloak subject without a local user account.");
            throw new UserLocalMissingException();
        }

        if (request.Channel == LoginChannel.Portal)
        {
            await EnsurePortalAccessAsync(request, user, token, cancellationToken).ConfigureAwait(false);
        }

        var roleName = await _userRepository
            .GetRoleNameAsync(user.RoleId, cancellationToken)
            .ConfigureAwait(false);

        await _auditLogger.LogAsync(
            AuditActionType.TokenRefresh,
            nameof(User),
            user.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: AuthAuditContext.Build(request.Channel, cause: null, request.ClientId),
            actorUserId: user.Id,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Session refreshed for user {UserId}.", user.Id);

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
    /// Aplica en la renovación las mismas reglas de acceso del portal que en el inicio de sesión. Un
    /// nutricionista suspendido después de entrar pierde la sesión en la siguiente renovación, a más
    /// tardar a los 15 minutos, cuando vence su access token (acta A68).
    /// </summary>
    /// <param name="request">Comando de renovación.</param>
    /// <param name="user">Cuenta local de la sesión.</param>
    /// <param name="token">Tokens recién renovados por Keycloak.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    /// <exception cref="InvalidRefreshTokenException">Si la cuenta ya no puede usar el portal.</exception>
    private async Task EnsurePortalAccessAsync(
        RefreshTokenCommand request,
        User user,
        KeycloakTokenResult token,
        CancellationToken cancellationToken)
    {
        var cause = await PortalAccessRules
            .FindRejectionCauseAsync(user, _userRepository, cancellationToken)
            .ConfigureAwait(false);
        if (cause is null)
        {
            return;
        }

        _logger.LogWarning("Portal session refresh rejected for user {UserId} ({Cause}).", user.Id, cause);

        try
        {
            await _tokenClient
                .LogoutAsync(token.RefreshToken, request.ClientId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not revoke the rejected portal session of user {UserId}.", user.Id);
        }

        await AuditFailureAsync(request, cause, user.Id, cancellationToken).ConfigureAwait(false);
        throw new InvalidRefreshTokenException();
    }

    /// <summary>
    /// Registra y confirma un intento de renovación rechazado. Se confirma aquí porque el handler termina
    /// con una excepción y nadie más llamaría a <c>SaveChangesAsync</c>.
    /// </summary>
    /// <param name="request">Comando de renovación.</param>
    /// <param name="cause">Causa interna del rechazo.</param>
    /// <param name="actorUserId">Cuenta afectada, si se pudo resolver con garantías.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    private async Task AuditFailureAsync(
        RefreshTokenCommand request,
        string cause,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        await _auditLogger.LogAsync(
            AuditActionType.FailedTokenRefresh,
            nameof(User),
            actorUserId,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: AuthAuditContext.Build(request.Channel, cause, request.ClientId),
            actorUserId: actorUserId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
