using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.Login;

/// <summary>
/// Handler del inicio de sesión. Delega la validación de credenciales a Keycloak y, si es exitosa,
/// actualiza la fecha del último acceso del usuario local. El evento LOGIN se audita en el
/// middleware; este handler no lo registra.
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IKeycloakTokenClient _tokenClient;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LoginCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public LoginCommandHandler(
        IKeycloakTokenClient tokenClient,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<LoginCommandHandler> logger)
    {
        _tokenClient = tokenClient;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var token = await _tokenClient
            .LoginAsync(request.Email, request.Password, request.ClientId, cancellationToken)
            .ConfigureAwait(false);

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
}
