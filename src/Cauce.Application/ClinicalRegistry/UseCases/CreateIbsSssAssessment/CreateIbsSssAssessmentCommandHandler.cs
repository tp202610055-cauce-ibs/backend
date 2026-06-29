using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Patients.UseCases.CompletePatientOnboarding;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateIbsSssAssessment;

/// <summary>
/// Handler del registro de una evaluación IBS-SSS. Asigna el número de ciclo, persiste,
/// audita el evento y, cuando es de línea base, cierra el onboarding del paciente. No
/// registra puntajes ni dimensiones en ningún log (PII clínica).
/// </summary>
public sealed class CreateIbsSssAssessmentCommandHandler : IRequestHandler<CreateIbsSssAssessmentCommand, CreateIbsSssAssessmentResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IIbsSssAssessmentRepository _assessmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ISender _mediator;
    private readonly ILogger<CreateIbsSssAssessmentCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CreateIbsSssAssessmentCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IIbsSssAssessmentRepository assessmentRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ISender mediator,
        ILogger<CreateIbsSssAssessmentCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _assessmentRepository = assessmentRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _mediator = mediator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateIbsSssAssessmentResult> Handle(CreateIbsSssAssessmentCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);

        int cycleNumber;
        if (request.AssessmentType == AssessmentType.Baseline)
        {
            var existingBaseline = await _assessmentRepository.FindBaselineByPatientAsync(patientId, cancellationToken).ConfigureAwait(false);
            if (existingBaseline is not null)
            {
                throw new DuplicateBaselineAssessmentException();
            }

            cycleNumber = 0;
        }
        else
        {
            cycleNumber = await _assessmentRepository.GetNextCycleNumberAsync(patientId, cancellationToken).ConfigureAwait(false);
        }

        var assessment = IbsSssAssessment.Submit(
            Guid.NewGuid(),
            patientId,
            request.AssessmentType,
            cycleNumber,
            request.PainSeverity,
            request.PainFrequency,
            request.BloatingSeverity,
            request.BowelHabitsDissatisfaction,
            request.LifeInterference,
            utcNow);

        await _assessmentRepository.AddAsync(assessment, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            AuditActionType.Create, nameof(IbsSssAssessment), assessment.Id,
            oldValuesHash: null, newValuesHash: null, additionalContext: null, cancellationToken).ConfigureAwait(false);

        var triggeredOnboardingCompletion = false;
        if (request.AssessmentType == AssessmentType.Baseline)
        {
            await _mediator.Send(new CompletePatientOnboardingCommand(), cancellationToken).ConfigureAwait(false);
            triggeredOnboardingCompletion = true;
        }

        _logger.LogInformation(
            "IBS-SSS assessment {AssessmentId} (cycle {CycleNumber}) registered for patient.",
            assessment.Id,
            assessment.CycleNumber);

        return new CreateIbsSssAssessmentResult(
            assessment.Id,
            assessment.TotalScore,
            assessment.SeverityCategory,
            assessment.NextAssessmentDate,
            triggeredOnboardingCompletion);
    }

    private async Task<Guid> ResolveCurrentPatientIdAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede registrar evaluaciones IBS-SSS.");
        }

        return user.Id;
    }
}
