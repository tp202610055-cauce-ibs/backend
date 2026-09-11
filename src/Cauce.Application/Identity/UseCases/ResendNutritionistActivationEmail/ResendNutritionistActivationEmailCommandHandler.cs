using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.ResendNutritionistActivationEmail;

/// <summary>
/// Handler del reenvío del enlace de activación de un nutricionista (acta A52). Solo procede con cuentas
/// pendientes de activación, y pide el envío a Keycloak, que es quien emite y procesa el enlace.
/// </summary>
/// <remarks>
/// A diferencia de la provisión, aquí un fallo de Keycloak <b>sí se propaga</b> como 502: el operador pidió
/// el reenvío explícitamente y tiene que saber si el correo salió. La auditoría se escribe después del
/// envío, así que la bitácora registra reenvíos efectivos, no intentos.
/// </remarks>
public sealed class ResendNutritionistActivationEmailCommandHandler
    : IRequestHandler<ResendNutritionistActivationEmailCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResendNutritionistActivationEmailCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ResendNutritionistActivationEmailCommandHandler(
        IUserRepository userRepository,
        IKeycloakAdminClient keycloakAdminClient,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        ILogger<ResendNutritionistActivationEmailCommandHandler> logger)
    {
        _userRepository = userRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(ResendNutritionistActivationEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository
            .FindByIdAsync(request.NutritionistId, cancellationToken)
            .ConfigureAwait(false);
        var nutritionistRoleId = await _userRepository
            .GetRoleIdAsync(UserRoles.Nutritionist, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || user.RoleId != nutritionistRoleId)
        {
            throw new NutritionistNotFoundException();
        }

        if (user.Status != UserStatus.PendingActivation)
        {
            throw new NutritionistNotPendingActivationException(user.Status);
        }

        await _keycloakAdminClient
            .SendUpdatePasswordEmailAsync(user.KeycloakId, cancellationToken)
            .ConfigureAwait(false);

        // users no tiene trigger: el reenvío queda en la bitácora solo si esta fila se escribe (DEC-B5-01
        // capa 3). El endpoint se autentica con clave de API, así que el actor viaja en el contexto.
        await _auditLogger.LogAsync(
            AuditActionType.ActivationEmailResend,
            nameof(User),
            user.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: JsonSerializer.Serialize(new { actor = "admin_api_key" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Activation link resent for nutritionist {UserId}.", user.Id);
    }
}
