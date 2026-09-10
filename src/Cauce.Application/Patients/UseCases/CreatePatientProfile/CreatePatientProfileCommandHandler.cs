using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Patients.UseCases.CreatePatientProfile;

/// <summary>
/// Handler de la creación del perfil clínico del paciente. Crea el perfil y, si el
/// paciente consumió un código de invitación, establece la asignación con su
/// nutricionista (cierra el flujo de vinculación por invitación).
/// </summary>
public sealed class CreatePatientProfileCommandHandler : IRequestHandler<CreatePatientProfileCommand, CreatePatientProfileResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IPatientProfileRepository _patientProfileRepository;
    private readonly IPatientNutritionistAssignmentService _assignmentService;
    private readonly IBmiCalculator _bmiCalculator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreatePatientProfileCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CreatePatientProfileCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IPatientProfileRepository patientProfileRepository,
        IPatientNutritionistAssignmentService assignmentService,
        IBmiCalculator bmiCalculator,
        IUnitOfWork unitOfWork,
        ILogger<CreatePatientProfileCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _patientProfileRepository = patientProfileRepository;
        _assignmentService = assignmentService;
        _bmiCalculator = bmiCalculator;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreatePatientProfileResult> Handle(CreatePatientProfileCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var user = await ResolvePatientAsync(cancellationToken).ConfigureAwait(false);

        if (await _patientProfileRepository.ExistsByUserIdAsync(user.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new DuplicatePatientProfileException();
        }

        var profile = PatientProfile.Create(
            Guid.NewGuid(),
            user.Id,
            request.DateOfBirth,
            request.BiologicalSex,
            request.WeightKg,
            request.HeightCm,
            request.IbsSubtype,
            request.DiagnosisDate,
            request.Medications,
            utcNow);

        await _patientProfileRepository.AddAsync(profile, cancellationToken).ConfigureAwait(false);

        var (assigned, assignmentId) = await _assignmentService
            .EstablishAssignmentAsync(user.Id, utcNow, cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // La auditoría de patient_profiles y nutritionist_patient la realizan los triggers de
        // PostgreSQL (DEC-B5-03).

        var bmi = _bmiCalculator.Calculate(profile.WeightKg, profile.HeightCm);
        var category = _bmiCalculator.Categorize(bmi);

        _logger.LogInformation("Patient profile {ProfileId} created (assignment: {Assigned}).", profile.Id, assigned);

        return new CreatePatientProfileResult(profile.Id, bmi, category, profile.GetAge(utcNow), assigned, assignmentId);
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
            throw new UnauthorizedAccessException("Solo un paciente puede crear su perfil clínico.");
        }

        return user;
    }

}
