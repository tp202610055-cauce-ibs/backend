using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;
using Cauce.Domain.Patients.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Patients.UseCases.AssignNutritionist;

/// <summary>
/// Handler del canje de un código de invitación después del registro (acta A41). Cierra el hueco
/// operativo de los pacientes que se registraron sin código y no tenían forma de vincularse luego.
/// </summary>
/// <remarks>
/// El orden de las comprobaciones es deliberado: <b>todo lo que puede rechazar el canje se verifica
/// antes de consumir el código</b>. Si el nutricionista no está disponible, el código queda intacto y
/// puede reutilizarse o reemitirse.
/// <para>
/// La auditoría explícita registra el <b>canje del código</b> (<c>invitation_codes</c>, tabla sin
/// trigger), no la asignación: la fila de <c>nutritionist_patient</c> ya la audita su trigger de
/// PostgreSQL, y duplicarla violaría la regla de no-duplicación de DEC-B5-01. Se audita tanto el canje
/// efectivo como el rechazado por nutricionista no disponible, con el estado exacto en el contexto.
/// </para>
/// </remarks>
public sealed class AssignNutritionistCommandHandler
    : IRequestHandler<AssignNutritionistCommand, AssignNutritionistResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IInvitationCodeRepository _invitationCodeRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IPatientNutritionistAssignmentService _assignmentService;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssignNutritionistCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public AssignNutritionistCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IInvitationCodeRepository invitationCodeRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IPatientNutritionistAssignmentService assignmentService,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        ILogger<AssignNutritionistCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _invitationCodeRepository = invitationCodeRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _assignmentService = assignmentService;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AssignNutritionistResult> Handle(
        AssignNutritionistCommand request,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var patient = await ResolvePatientAsync(cancellationToken).ConfigureAwait(false);

        // 1. Un paciente ya vinculado no puede canjear otro código: cambiar de nutricionista es una
        //    decisión clínica, no el efecto de pegar un código (decisión D5).
        var existing = await _nutritionistPatientRepository
            .FindActiveByPatientAsync(patient.Id, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            throw new PatientAlreadyAssignedException();
        }

        // 2. Resolución y vigencia del código, con las mismas reglas que el registro.
        var invitation = await _invitationCodeRepository
            .FindByCodeAsync(request.InvitationCode, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidInvitationCodeException();

        if (!invitation.IsValid(utcNow))
        {
            if (invitation.ExpiresAt <= utcNow)
            {
                throw new ExpiredInvitationCodeException();
            }

            throw new InvitationCodeAlreadyUsedException();
        }

        // 3. El nutricionista debe poder atender. Se verifica ANTES de consumir el código.
        var nutritionist = await _userRepository
            .FindByIdAsync(invitation.NutritionistId, cancellationToken)
            .ConfigureAwait(false);
        if (nutritionist is null)
        {
            // Código válido apuntando a una cuenta inexistente: inconsistencia de datos, no un problema
            // del paciente. Se trata como código inválido para no filtrar el estado interno.
            _logger.LogCritical(
                "Invitation code {InvitationCodeId} references a missing nutritionist {NutritionistId}.",
                invitation.Id,
                invitation.NutritionistId);
            throw new InvalidInvitationCodeException();
        }

        if (nutritionist.Status != UserStatus.Active)
        {
            await AuditRedemptionAsync(
                patient.Id, invitation.Id, nutritionist, outcome: "rejected", cancellationToken).ConfigureAwait(false);

            // Se confirma solo la bitácora: hasta aquí no se modificó nada de negocio, así que esta
            // transacción escribe la fila de auditoría y nada más.
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            throw new NutritionistNotAvailableException(nutritionist.Status);
        }

        // 4. Consumir el código y establecer el vínculo. Son dos pasos porque en el registro ocurren en
        //    momentos distintos; el canje post-registro los necesita juntos (acta A41).
        await _assignmentService
            .ConsumeInvitationAsync(patient.Id, invitation, utcNow, LinkContext.PostRegistrationLink, cancellationToken)
            .ConfigureAwait(false);

        // Se pasa el código ya resuelto: su MarkAsUsed todavía no se persistió, así que buscarlo por
        // paciente en la base no encontraría nada y el vínculo no se crearía.
        var (assigned, _) = await _assignmentService
            .EstablishAssignmentAsync(patient.Id, invitation, utcNow, cancellationToken)
            .ConfigureAwait(false);
        if (!assigned)
        {
            // No debería ocurrir: ya se verificó que el paciente no tiene asignación activa.
            _logger.LogError(
                "Assignment was not established for patient {PatientId} after consuming code {InvitationCodeId}.",
                patient.Id,
                invitation.Id);
            throw new NutritionistAssignmentAlreadyExistsException();
        }

        await AuditRedemptionAsync(
            patient.Id, invitation.Id, nutritionist, outcome: "assigned", cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Patient {PatientId} redeemed invitation {InvitationCodeId} and was assigned to nutritionist {NutritionistId}.",
            patient.Id,
            invitation.Id,
            nutritionist.Id);

        return new AssignNutritionistResult(nutritionist.Id, nutritionist.FullName, utcNow);
    }

    /// <summary>
    /// Resuelve al paciente autenticado, con las mismas comprobaciones que el resto de los casos de uso
    /// de pacientes.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El usuario autenticado, garantizado paciente.</returns>
    private async Task<User> ResolvePatientAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede canjear un código de invitación.");
        }

        return user;
    }

    /// <summary>
    /// Registra el intento de canje en la bitácora, con el estado del nutricionista, para permitir
    /// investigar después tanto los canjes efectivos como los rechazados (decisión D7).
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="invitationCodeId">Identificador del código canjeado.</param>
    /// <param name="nutritionist">Nutricionista dueño del código.</param>
    /// <param name="outcome">Desenlace del canje: <c>assigned</c> o <c>rejected</c>.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    private Task AuditRedemptionAsync(
        Guid patientId,
        Guid invitationCodeId,
        User nutritionist,
        string outcome,
        CancellationToken ct)
    {
        var context = JsonSerializer.Serialize(new
        {
            outcome,
            patientId,
            nutritionistId = nutritionist.Id,
            nutritionistStatus = nutritionist.Status.ToString(),
            invitationCodeId
        });

        return _auditLogger.LogAsync(
            AuditActionType.NutritionistAssignment,
            nameof(InvitationCode),
            invitationCodeId,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: context,
            actorUserId: patientId,
            cancellationToken: ct);
    }
}
