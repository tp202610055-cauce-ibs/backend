using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Mapping;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Exceptions;
using MediatR;

namespace Cauce.Application.Patients.UseCases.GetAssignedPatientDetail;

/// <summary>
/// Handler que devuelve el detalle clínico de un paciente asignado. Rechaza el
/// acceso si el nutricionista no tiene una asignación activa con el paciente.
/// </summary>
public sealed class GetAssignedPatientDetailQueryHandler : IRequestHandler<GetAssignedPatientDetailQuery, GetAssignedPatientDetailResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IPatientProfileRepository _patientProfileRepository;
    private readonly IPatientAllergyRepository _patientAllergyRepository;
    private readonly IAllergyRepository _allergyRepository;
    private readonly IBmiCalculator _bmiCalculator;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetAssignedPatientDetailQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IPatientProfileRepository patientProfileRepository,
        IPatientAllergyRepository patientAllergyRepository,
        IAllergyRepository allergyRepository,
        IBmiCalculator bmiCalculator)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _patientProfileRepository = patientProfileRepository;
        _patientAllergyRepository = patientAllergyRepository;
        _allergyRepository = allergyRepository;
        _bmiCalculator = bmiCalculator;
    }

    /// <inheritdoc />
    public async Task<GetAssignedPatientDetailResult> Handle(GetAssignedPatientDetailQuery request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var nutritionist = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var nutritionistRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, cancellationToken).ConfigureAwait(false);
        if (nutritionist.RoleId != nutritionistRoleId)
        {
            throw new UnauthorizedAccessException("Solo un nutricionista puede consultar el detalle de un paciente.");
        }

        var hasAccess = await _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(nutritionist.Id, request.PatientUserId, cancellationToken)
            .ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new PatientAccessNotAuthorizedException();
        }

        var patient = await _userRepository.FindByIdAsync(request.PatientUserId, cancellationToken).ConfigureAwait(false)
            ?? throw new PatientProfileNotFoundException();

        var profile = await _patientProfileRepository.FindByUserIdAsync(request.PatientUserId, cancellationToken).ConfigureAwait(false)
            ?? throw new PatientProfileNotFoundException();

        var declarations = await _patientAllergyRepository.ListByPatientAsync(request.PatientUserId, cancellationToken).ConfigureAwait(false);
        var allergies = await PatientAllergyMapper.MapAsync(declarations, _allergyRepository, cancellationToken).ConfigureAwait(false);

        var bmi = _bmiCalculator.Calculate(profile.WeightKg, profile.HeightCm);

        return new GetAssignedPatientDetailResult(
            patient.Id,
            patient.FullName,
            profile.GetAge(utcNow),
            bmi,
            _bmiCalculator.Categorize(bmi),
            profile.IbsSubtype,
            profile.OnboardingCompleted,
            allergies);
    }
}
