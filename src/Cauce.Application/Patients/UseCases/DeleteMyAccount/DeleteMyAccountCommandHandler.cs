using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Patients.UseCases.DeleteMyAccount;

/// <summary>
/// Handler de la eliminación (anonimización) de la cuenta del paciente autenticado (US26). No realiza
/// borrado físico: anonimiza los datos personales del <see cref="User"/>, deshabilita la cuenta en
/// Keycloak y conserva las filas clínicas y de consentimiento para preservar la trazabilidad exigida
/// por la Ley N° 29733.
/// </summary>
public sealed class DeleteMyAccountCommandHandler : IRequestHandler<DeleteMyAccountCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly IEmailSender _emailSender;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteMyAccountCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public DeleteMyAccountCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IKeycloakAdminClient keycloakAdminClient,
        IEmailSender emailSender,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        ILogger<DeleteMyAccountCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _emailSender = emailSender;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(DeleteMyAccountCommand request, CancellationToken cancellationToken)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, cancellationToken).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede eliminar su propia cuenta.");
        }

        // US26 CA02: un paciente en el piloto activo debe acusar la retención normativa para continuar.
        if (user.IsInActivePilot && !request.ConfirmedActivePilotAcknowledged)
        {
            throw new ActivePilotRetentionException();
        }

        var originalEmail = user.Email;
        var originalFullName = user.FullName;
        var originalKeycloakId = user.KeycloakId;

        user.Anonymize(patientRoleId);

        // Se deshabilita en Keycloak ANTES de confirmar la transacción local: si la operación externa
        // falla, la anonimización no se persiste y la baja es reintentable (acta A17).
        await _keycloakAdminClient.DisableUserAsync(originalKeycloakId, cancellationToken).ConfigureAwait(false);

        // Auditoría explícita ANTES del SaveChanges: la tabla users no tiene trigger, así que la
        // anonimización y su bitácora se persisten en una sola transacción (DEC-B5-01 capa 3).
        await _auditLogger.LogAsync(
            AuditActionType.Delete,
            nameof(User),
            user.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: JsonSerializer.Serialize(new { operation = "anonymization", keycloak_action = "disable" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TrySendConfirmationAsync(originalEmail, originalFullName, user.Id, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Patient account {UserId} anonymized on user request.", user.Id);

        return Unit.Value;
    }

    private async Task TrySendConfirmationAsync(string email, string fullName, Guid userId, CancellationToken ct)
    {
        try
        {
            await _emailSender.SendAccountDeletionConfirmationAsync(email, fullName, ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not send the account deletion confirmation email for user {UserId}; the anonymization remains valid.",
                userId);
        }
    }
}
