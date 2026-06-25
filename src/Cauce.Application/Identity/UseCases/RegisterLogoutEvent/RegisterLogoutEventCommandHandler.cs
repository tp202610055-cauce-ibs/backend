using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.RegisterLogoutEvent;

/// <summary>
/// Handler que registra el cierre de sesión del usuario autenticado en la bitácora
/// de auditoría. No modifica la entidad de usuario.
/// </summary>
public sealed class RegisterLogoutEventCommandHandler : IRequestHandler<RegisterLogoutEventCommand>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<RegisterLogoutEventCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public RegisterLogoutEventCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IAuditLogger auditLogger,
        ILogger<RegisterLogoutEventCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(RegisterLogoutEventCommand request, CancellationToken cancellationToken)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            AuditActionType.Logout,
            nameof(User),
            user?.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: null,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Logout event registered for keycloak subject.");
    }
}
