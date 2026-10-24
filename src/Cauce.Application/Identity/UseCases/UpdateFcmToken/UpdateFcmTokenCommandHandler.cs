using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.UpdateFcmToken;

/// <summary>
/// Handler del registro del token de FCM del dispositivo del usuario autenticado (TS10 CA01). Aplica a
/// cualquier rol. La tabla <c>users</c> no tiene trigger de auditoría, por lo que se audita de forma
/// explícita antes del <c>SaveChanges</c> (coherencia con el patrón del bloque 5, acta A20).
/// </summary>
public sealed class UpdateFcmTokenCommandHandler : IRequestHandler<UpdateFcmTokenCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateFcmTokenCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public UpdateFcmTokenCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        ILogger<UpdateFcmTokenCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(UpdateFcmTokenCommand request, CancellationToken cancellationToken)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        user.RegisterFcmToken(request.FcmToken);

        // Un teléfono tiene un solo token: si otra cuenta lo tenía (cambio de sesión en el mismo equipo),
        // se le quita en la misma transacción para que sus push no lleguen a este teléfono.
        if (user.FcmToken is { } fcmToken)
        {
            var previousOwners = await _userRepository
                .ListOthersWithFcmTokenAsync(fcmToken, user.Id, cancellationToken)
                .ConfigureAwait(false);
            foreach (var previousOwner in previousOwners)
            {
                previousOwner.RegisterFcmToken(null);
                await AuditAsync(previousOwner.Id, "fcm_token_moved_to_another_account", cancellationToken).ConfigureAwait(false);
            }
        }

        await AuditAsync(user.Id, "fcm_token_updated", cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("FCM token updated for user {UserId}.", user.Id);

        return Unit.Value;
    }

    private Task AuditAsync(Guid userId, string operation, CancellationToken cancellationToken)
    {
        return _auditLogger.LogAsync(
            AuditActionType.Update,
            nameof(User),
            userId,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: JsonSerializer.Serialize(new { operation }),
            cancellationToken: cancellationToken);
    }
}
