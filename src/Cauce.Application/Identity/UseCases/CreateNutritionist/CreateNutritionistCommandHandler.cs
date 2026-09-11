using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.CreateNutritionist;

/// <summary>
/// Handler de la provisión de nutricionistas. Crea el usuario en Keycloak sin contraseña, persiste la
/// cuenta local pendiente de activación y pide a Keycloak que envíe el enlace con el que el propio
/// nutricionista define su contraseña (acta A52). Ante un fallo de persistencia posterior a la creación
/// en Keycloak, compensa eliminando el usuario.
/// </summary>
/// <remarks>
/// El enlace se pide <b>después</b> de confirmar la transacción y en modo best-effort: si falla, la cuenta
/// queda provisionada y el resultado lo informa, en vez de compensar. Compensar a esa altura borraría el
/// usuario de Keycloak con la fila local ya confirmada, que es justamente la cuenta huérfana que producía
/// el envío de credenciales anterior. El enlace se vuelve a pedir con el endpoint de reenvío.
/// </remarks>
public sealed class CreateNutritionistCommandHandler : IRequestHandler<CreateNutritionistCommand, CreateNutritionistResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<CreateNutritionistCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CreateNutritionistCommandHandler(
        IUserRepository userRepository,
        IKeycloakAdminClient keycloakAdminClient,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<CreateNutritionistCommandHandler> logger)
    {
        _userRepository = userRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateNutritionistResult> Handle(CreateNutritionistCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false))
        {
            throw new DuplicateEmailException();
        }

        var nutritionistRoleId = await _userRepository
            .GetRoleIdAsync(UserRoles.Nutritionist, cancellationToken)
            .ConfigureAwait(false);

        // Sin contraseña: la define el propio nutricionista con el enlace de Keycloak. Hasta entonces no
        // puede autenticarse, y ninguna credencial viaja por correo.
        var keycloakId = await _keycloakAdminClient
            .CreateUserAsync(request.Email, request.FullName, UserRoles.Nutritionist, requireEmailVerification: false, cancellationToken)
            .ConfigureAwait(false);

        User user;
        try
        {
            user = User.CreateNutritionist(Guid.NewGuid(), keycloakId, request.Email, request.FullName, nutritionistRoleId);
            await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);

            // Auditoría explícita ANTES del SaveChanges: users no tiene trigger; una sola transacción
            // persiste la cuenta y la bitácora de forma atómica (DEC-B5-01 capa 3, acta A8).
            await _auditLogger.LogAsync(
                AuditActionType.Register,
                nameof(User),
                user.Id,
                oldValuesHash: null,
                newValuesHash: null,
                additionalContext: JsonSerializer.Serialize(new { actor = "admin_api_key", role = UserRoles.Nutritionist }),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Nutritionist provisioning failed after Keycloak user creation; compensating by deleting Keycloak user.");
            await CompensateKeycloakAsync(keycloakId, cancellationToken).ConfigureAwait(false);
            throw;
        }

        var activationEmailSent = await TrySendActivationEmailAsync(keycloakId, user.Id, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Nutritionist {UserId} provisioned successfully.", user.Id);

        return new CreateNutritionistResult(user.Id, user.Email, user.Status, activationEmailSent);
    }

    /// <summary>
    /// Pide a Keycloak el enlace para definir la contraseña. Es best-effort: la cuenta ya está confirmada y
    /// un fallo del proveedor no debe deshacerla, solo quedar informado en el resultado.
    /// </summary>
    /// <param name="keycloakId">Identificador del usuario en Keycloak.</param>
    /// <param name="userId">Identificador local del usuario, para la traza.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si Keycloak aceptó el envío.</returns>
    private async Task<bool> TrySendActivationEmailAsync(string keycloakId, Guid userId, CancellationToken ct)
    {
        try
        {
            await _keycloakAdminClient.SendUpdatePasswordEmailAsync(keycloakId, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Could not request the activation link for nutritionist {UserId}; it can be resent.",
                userId);
            return false;
        }
    }

    private async Task CompensateKeycloakAsync(string keycloakId, CancellationToken ct)
    {
        try
        {
            await _keycloakAdminClient.DeleteUserAsync(keycloakId, ct).ConfigureAwait(false);
        }
        catch (Exception compensationException)
        {
            _logger.LogError(
                compensationException,
                "Compensation failed: could not delete Keycloak user after a failed provisioning.");
        }
    }
}
