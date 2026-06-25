using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.RequestPasswordReset;

/// <summary>
/// Handler de la solicitud de restablecimiento de contraseña. Si el correo no
/// corresponde a ninguna cuenta, finaliza silenciosamente sin generar token ni
/// enviar correo, para no revelar la existencia de cuentas.
/// </summary>
public sealed class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenGenerator _tokenGenerator;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IClientUrlProvider _clientUrlProvider;
    private readonly IEmailSender _emailSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<RequestPasswordResetCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public RequestPasswordResetCommandHandler(
        IUserRepository userRepository,
        IPasswordResetTokenGenerator tokenGenerator,
        IPasswordResetTokenRepository tokenRepository,
        IClientUrlProvider clientUrlProvider,
        IEmailSender emailSender,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<RequestPasswordResetCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tokenGenerator = tokenGenerator;
        _tokenRepository = tokenRepository;
        _clientUrlProvider = clientUrlProvider;
        _emailSender = emailSender;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var (plainToken, tokenHash) = _tokenGenerator.GeneratePair();

        var token = PasswordResetToken.Issue(Guid.NewGuid(), user.Id, tokenHash, utcNow, PasswordResetToken.Validity);
        await _tokenRepository.AddAsync(token, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var resetLink = _clientUrlProvider.BuildPasswordResetLink(plainToken);
        await _emailSender
            .SendPasswordResetLinkAsync(user.Email, user.FullName, resetLink, cancellationToken)
            .ConfigureAwait(false);

        await _auditLogger.LogAsync(
            AuditActionType.PasswordResetRequest,
            nameof(User),
            user.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: null,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Password reset requested for user {UserId}.", user.Id);
    }
}
