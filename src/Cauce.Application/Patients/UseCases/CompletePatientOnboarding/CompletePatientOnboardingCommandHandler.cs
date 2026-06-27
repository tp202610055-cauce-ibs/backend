using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Patients.UseCases.CompletePatientOnboarding;

/// <summary>
/// Handler que marca el onboarding del paciente autenticado como completado. La
/// operación es idempotente a nivel de dominio.
/// </summary>
public sealed class CompletePatientOnboardingCommandHandler : IRequestHandler<CompletePatientOnboardingCommand>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IPatientProfileRepository _patientProfileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<CompletePatientOnboardingCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CompletePatientOnboardingCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IPatientProfileRepository patientProfileRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<CompletePatientOnboardingCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _patientProfileRepository = patientProfileRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(CompletePatientOnboardingCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var profile = await _patientProfileRepository.FindByUserIdAsync(user.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new PatientProfileNotFoundException();

        if (profile.OnboardingCompleted)
        {
            return;
        }

        profile.CompleteOnboarding(utcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            AuditActionType.Update, nameof(PatientProfile), profile.Id,
            oldValuesHash: null, newValuesHash: null, additionalContext: null, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Patient onboarding completed for profile {ProfileId}.", profile.Id);
    }
}
