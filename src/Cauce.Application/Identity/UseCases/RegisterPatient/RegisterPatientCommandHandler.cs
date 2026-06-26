using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.UseCases.RegisterPatient;

/// <summary>
/// Handler del registro de paciente. Crea el usuario en Keycloak antes de
/// persistir localmente y, ante un fallo posterior, ejecuta una compensación que
/// elimina el usuario recién creado en Keycloak para evitar inconsistencias.
/// </summary>
public sealed class RegisterPatientCommandHandler : IRequestHandler<RegisterPatientCommand, RegisterPatientResult>
{
    private readonly IConsentService _consentService;
    private readonly IUserRepository _userRepository;
    private readonly IInvitationCodeRepository _invitationCodeRepository;
    private readonly IConsentRecordRepository _consentRecordRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<RegisterPatientCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public RegisterPatientCommandHandler(
        IConsentService consentService,
        IUserRepository userRepository,
        IInvitationCodeRepository invitationCodeRepository,
        IConsentRecordRepository consentRecordRepository,
        IKeycloakAdminClient keycloakAdminClient,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<RegisterPatientCommandHandler> logger)
    {
        _consentService = consentService;
        _userRepository = userRepository;
        _invitationCodeRepository = invitationCodeRepository;
        _consentRecordRepository = consentRecordRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RegisterPatientResult> Handle(RegisterPatientCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        if (!_consentService.VerifyHash(request.ConsentDocumentVersion, request.ConsentTextHash))
        {
            throw new ConsentTextMismatchException();
        }

        if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false))
        {
            throw new DuplicateEmailException();
        }

        var invitation = await ResolveInvitationAsync(request.InvitationCode, utcNow, cancellationToken).ConfigureAwait(false);

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, cancellationToken).ConfigureAwait(false);

        var keycloakId = await _keycloakAdminClient
            .CreateUserAsync(request.Email, request.FullName, UserRoles.Patient, requireEmailVerification: true, cancellationToken)
            .ConfigureAwait(false);

        User user;
        try
        {
            await _keycloakAdminClient.ResetPasswordAsync(keycloakId, request.Password, cancellationToken).ConfigureAwait(false);

            user = User.CreatePatient(Guid.NewGuid(), keycloakId, request.Email, request.FullName, patientRoleId);
            await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);

            var consent = ConsentRecord.Capture(
                Guid.NewGuid(),
                user.Id,
                request.ConsentDocumentVersion,
                request.ConsentTextHash,
                request.IpAddress,
                utcNow);
            await _consentRecordRepository.AddAsync(consent, cancellationToken).ConfigureAwait(false);

            invitation?.MarkAsUsed(user.Id, utcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _auditLogger.LogAsync(
                AuditActionType.Register,
                nameof(User),
                user.Id,
                oldValuesHash: null,
                newValuesHash: null,
                additionalContext: JsonSerializer.Serialize(new { role = UserRoles.Patient }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Patient registration failed after Keycloak user creation; compensating by deleting Keycloak user.");
            await CompensateKeycloakAsync(keycloakId, cancellationToken).ConfigureAwait(false);
            throw;
        }

        await TrySendVerifyEmailAsync(keycloakId, user.Id, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Patient {UserId} registered successfully.", user.Id);

        return new RegisterPatientResult(user.Id, user.Email, user.Status, EmailVerificationRequired: true);
    }

    private async Task<InvitationCode?> ResolveInvitationAsync(string? code, DateTime utcNow, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var invitation = await _invitationCodeRepository.FindByCodeAsync(code, ct).ConfigureAwait(false)
            ?? throw new InvalidInvitationCodeException();

        if (!invitation.IsValid(utcNow))
        {
            if (invitation.ExpiresAt <= utcNow)
            {
                throw new ExpiredInvitationCodeException();
            }

            throw new InvitationCodeAlreadyUsedException();
        }

        return invitation;
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
                "Compensation failed: could not delete Keycloak user after a failed registration.");
        }
    }

    private async Task TrySendVerifyEmailAsync(string keycloakId, Guid userId, CancellationToken ct)
    {
        try
        {
            await _keycloakAdminClient.SendVerifyEmailAsync(keycloakId, ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not trigger the verification email for user {UserId}; registration remains valid.",
                userId);
        }
    }
}
