using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Application.Patients.Mapping;
using Cauce.Domain.Patients.Exceptions;
using MediatR;

namespace Cauce.Application.Patients.UseCases.GetPatientProfile;

/// <summary>
/// Handler que arma el perfil clínico completo del paciente autenticado.
/// </summary>
public sealed class GetPatientProfileQueryHandler : IRequestHandler<GetPatientProfileQuery, GetPatientProfileResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IPatientProfileRepository _patientProfileRepository;
    private readonly IPatientAllergyRepository _patientAllergyRepository;
    private readonly IAllergyRepository _allergyRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IBmiCalculator _bmiCalculator;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetPatientProfileQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IPatientProfileRepository patientProfileRepository,
        IPatientAllergyRepository patientAllergyRepository,
        IAllergyRepository allergyRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IBmiCalculator bmiCalculator)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _patientProfileRepository = patientProfileRepository;
        _patientAllergyRepository = patientAllergyRepository;
        _allergyRepository = allergyRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _bmiCalculator = bmiCalculator;
    }

    /// <inheritdoc />
    public async Task<GetPatientProfileResult> Handle(GetPatientProfileQuery request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var profile = await _patientProfileRepository.FindByUserIdAsync(user.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new PatientProfileNotFoundException();

        var declarations = await _patientAllergyRepository.ListByPatientAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var allergies = await PatientAllergyMapper.MapAsync(declarations, _allergyRepository, cancellationToken).ConfigureAwait(false);

        var assignment = await _nutritionistPatientRepository.FindActiveByPatientAsync(user.Id, cancellationToken).ConfigureAwait(false);
        NutritionistAssignmentSummary? assignedNutritionist = null;
        if (assignment is not null)
        {
            var nutritionist = await _userRepository.FindByIdAsync(assignment.NutritionistId, cancellationToken).ConfigureAwait(false);
            assignedNutritionist = new NutritionistAssignmentSummary(
                assignment.Id,
                assignment.NutritionistId,
                nutritionist?.FullName ?? string.Empty,
                assignment.AssignedAt);
        }

        var bmi = _bmiCalculator.Calculate(profile.WeightKg, profile.HeightCm);

        return new GetPatientProfileResult(
            profile.Id,
            profile.UserId,
            profile.DateOfBirth,
            profile.BiologicalSex,
            profile.WeightKg,
            profile.HeightCm,
            bmi,
            _bmiCalculator.Categorize(bmi),
            profile.GetAge(utcNow),
            profile.IbsSubtype,
            profile.DiagnosisDate,
            profile.Medications,
            profile.OnboardingCompleted,
            allergies,
            assignedNutritionist);
    }
}
