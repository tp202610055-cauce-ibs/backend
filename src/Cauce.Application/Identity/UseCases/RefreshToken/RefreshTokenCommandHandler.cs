using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
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
/// handler sí conoce la cuenta, y la pasa explícitamente.
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
            await AuditAsync(AuditActionType.FailedTokenRefresh, actorUserId: null, request.ClientId, cancellationToken)
                .ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        var roleName = await _userRepository
            .GetRoleNameAsync(user.RoleId, cancellationToken)
            .ConfigureAwait(false);

        await AuditAsync(AuditActionType.TokenRefresh, user.Id, request.ClientId, cancellationToken)
            .ConfigureAwait(false);
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

    private Task AuditAsync(
        AuditActionType actionType,
        Guid? actorUserId,
        string clientId,
        CancellationToken cancellationToken)
    {
        return _auditLogger.LogAsync(
            actionType,
            nameof(User),
            actorUserId,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: JsonSerializer.Serialize(new { clientId }),
            actorUserId: actorUserId,
            cancellationToken: cancellationToken);
    }
}
