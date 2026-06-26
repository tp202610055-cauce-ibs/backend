using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.RegisterLoginEvent;

/// <summary>
/// Handler que registra un inicio de sesión exitoso del usuario autenticado:
/// actualiza la fecha de último acceso y deja constancia en la bitácora de auditoría.
/// </summary>
public sealed class RegisterLoginEventCommandHandler : IRequestHandler<RegisterLoginEventCommand>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<RegisterLoginEventCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public RegisterLoginEventCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<RegisterLoginEventCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(RegisterLoginEventCommand request, CancellationToken cancellationToken)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El usuario autenticado no tiene una cuenta local asociada.");

        user.RegisterSuccessfulLogin(DateTime.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            AuditActionType.Login,
            nameof(User),
            user.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: null,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Login event registered for user {UserId}.", user.Id);
    }
}
