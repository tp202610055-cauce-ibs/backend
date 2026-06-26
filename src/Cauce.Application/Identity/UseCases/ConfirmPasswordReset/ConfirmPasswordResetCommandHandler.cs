using System.Security.Cryptography;
using System.Text;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.ConfirmPasswordReset;

/// <summary>
/// Handler de la confirmación de restablecimiento de contraseña. Verifica el token,
/// delega el cambio de contraseña a Keycloak y consume el token de forma atómica.
/// </summary>
public sealed class ConfirmPasswordResetCommandHandler : IRequestHandler<ConfirmPasswordResetCommand>
{
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<ConfirmPasswordResetCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ConfirmPasswordResetCommandHandler(
        IPasswordResetTokenRepository tokenRepository,
        IUserRepository userRepository,
        IKeycloakAdminClient keycloakAdminClient,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<ConfirmPasswordResetCommandHandler> logger)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(ConfirmPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var tokenHash = ComputeHash(request.Token);

        var token = await _tokenRepository.FindByTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidPasswordResetTokenException();

        if (token.IsUsed)
        {
            throw new InvalidPasswordResetTokenException();
        }

        if (token.IsExpired(utcNow))
        {
            throw new ExpiredPasswordResetTokenException();
        }

        var user = await _userRepository.FindByIdAsync(token.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidPasswordResetTokenException();

        await _keycloakAdminClient
            .ResetPasswordAsync(user.KeycloakId, request.NewPassword, cancellationToken)
            .ConfigureAwait(false);

        token.Consume(utcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            AuditActionType.PasswordResetConfirm,
            nameof(User),
            user.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: null,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Password reset confirmed for user {UserId}.", user.Id);
    }

    private static string ComputeHash(string plainToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
