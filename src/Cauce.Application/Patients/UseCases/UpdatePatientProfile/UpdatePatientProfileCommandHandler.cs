using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Events;
using Cauce.Domain.Patients.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Patients.UseCases.UpdatePatientProfile;

/// <summary>
/// Handler de la actualización del perfil clínico del paciente autenticado. Aplica
/// únicamente los campos provistos.
/// </summary>
public sealed class UpdatePatientProfileCommandHandler : IRequestHandler<UpdatePatientProfileCommand, UpdatePatientProfileResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IPatientProfileRepository _patientProfileRepository;
    private readonly IBmiCalculator _bmiCalculator;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdatePatientProfileCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public UpdatePatientProfileCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IPatientProfileRepository patientProfileRepository,
        IBmiCalculator bmiCalculator,
        IOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        ILogger<UpdatePatientProfileCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _patientProfileRepository = patientProfileRepository;
        _bmiCalculator = bmiCalculator;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdatePatientProfileResult> Handle(UpdatePatientProfileCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var user = await ResolvePatientAsync(cancellationToken).ConfigureAwait(false);

        var profile = await _patientProfileRepository.FindByUserIdAsync(user.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new PatientProfileNotFoundException();

        if (request.WeightKg.HasValue || request.HeightCm.HasValue)
        {
            profile.UpdateBiometrics(
                request.WeightKg ?? profile.WeightKg,
                request.HeightCm ?? profile.HeightCm,
                utcNow);
        }

        if (request.IbsSubtype.HasValue)
        {
            var oldSubtype = profile.IbsSubtype;
            profile.UpdateIbsSubtype(request.IbsSubtype.Value, utcNow);

            // US03 CA03: notificar al nutricionista asignado solo si el subtipo efectivamente cambió.
            // La notificación se agenda de forma asíncrona vía outbox (patrón DEC-B5-04).
            if (oldSubtype != request.IbsSubtype.Value)
            {
                await _outboxWriter.PublishAsync(
                    profile.Id,
                    nameof(PatientProfile),
                    new PatientSubtypeChangedEvent(user.Id, oldSubtype, request.IbsSubtype.Value, utcNow),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (request.DiagnosisDate.HasValue)
        {
            profile.UpdateDiagnosisDate(request.DiagnosisDate, utcNow);
        }

        if (request.Medications is not null)
        {
            profile.UpdateMedications(request.Medications, utcNow);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // La auditoría de patient_profiles la realiza el trigger de PostgreSQL (DEC-B5-03).

        var bmi = _bmiCalculator.Calculate(profile.WeightKg, profile.HeightCm);
        _logger.LogInformation("Patient profile {ProfileId} updated.", profile.Id);

        return new UpdatePatientProfileResult(bmi, _bmiCalculator.Categorize(bmi), profile.GetAge(utcNow));
    }

    private async Task<User> ResolvePatientAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede modificar su perfil clínico.");
        }

        return user;
    }
}
