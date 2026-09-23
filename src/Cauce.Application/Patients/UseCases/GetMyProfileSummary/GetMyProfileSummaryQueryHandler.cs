using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Application.Patients.Mapping;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Exceptions;
using MediatR;

namespace Cauce.Application.Patients.UseCases.GetMyProfileSummary;

/// <summary>
/// Handler del perfil agregado del paciente (US28). Resuelve al paciente, su código correlativo, su
/// perfil clínico con las alergias declaradas, el nutricionista asignado y su evolución IBS-SSS
/// (línea base, más reciente, cambio acumulado). El correo se devuelve enmascarado y la fecha de
/// inicio en el piloto se resuelve por precedencia.
///
/// <para>Es un agregador de lectura: junta en una respuesta lo que la pantalla de perfil necesita
/// para no encadenar llamadas. De ahí que tenga más dependencias que un handler típico.</para>
/// </summary>
public sealed class GetMyProfileSummaryQueryHandler : IRequestHandler<GetMyProfileSummaryQuery, MyProfileSummaryResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IPatientProfileRepository _patientProfileRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IIbsSssAssessmentRepository _assessmentRepository;
    private readonly IAllergyRepository _allergyRepository;
    private readonly IPatientAllergyRepository _patientAllergyRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetMyProfileSummaryQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IPatientProfileRepository patientProfileRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IIbsSssAssessmentRepository assessmentRepository,
        IAllergyRepository allergyRepository,
        IPatientAllergyRepository patientAllergyRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _patientProfileRepository = patientProfileRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _assessmentRepository = assessmentRepository;
        _allergyRepository = allergyRepository;
        _patientAllergyRepository = patientAllergyRepository;
    }

    /// <inheritdoc />
    public async Task<MyProfileSummaryResult> Handle(GetMyProfileSummaryQuery request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, cancellationToken).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede consultar su perfil agregado.");
        }

        var profile = await _patientProfileRepository.FindByUserIdAsync(user.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new PatientProfileNotFoundException();

        var baseline = await _assessmentRepository.FindBaselineByPatientAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var latest = await _assessmentRepository.FindLatestByPatientAsync(user.Id, cancellationToken).ConfigureAwait(false);

        // Se reutiliza el mapeador del endpoint de alergias para que el resumen devuelva exactamente
        // la misma forma que GET /patients/allergies y GET /patients/profile, y no una tercera.
        var declarations = await _patientAllergyRepository.ListByPatientAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var allergies = await PatientAllergyMapper
            .MapAsync(declarations, _allergyRepository, cancellationToken)
            .ConfigureAwait(false);

        var assignment = await _nutritionistPatientRepository.FindActiveByPatientAsync(user.Id, cancellationToken).ConfigureAwait(false);
        NutritionistAssignmentSummary? assignedNutritionist = null;
        if (assignment is not null)
        {
            var nutritionist = await _userRepository.FindByIdAsync(assignment.NutritionistId, cancellationToken).ConfigureAwait(false);
            assignedNutritionist = new NutritionistAssignmentSummary(
                assignment.Id, assignment.NutritionistId, nutritionist?.FullName ?? string.Empty, assignment.AssignedAt);
        }

        int? cumulativeChange = baseline is not null && latest is not null
            ? latest.CompareTotalScoreTo(baseline)
            : null;
        var significantResponse = baseline is not null && latest is not null && latest.IsClinicallySignificantImprovement(baseline);

        return new MyProfileSummaryResult(
            // El código lo asigna el alta y es obligatorio en User.CreatePatient, así que para un
            // paciente nunca falta; el fallback cubre una fila insertada fuera del dominio y evita
            // que el cliente reciba null en un campo que el contrato declara presente.
            new MyProfilePatientInfo(user.PatientCode ?? string.Empty, user.FullName, MaskEmail(user.Email)),
            new MyProfileClinicalInfo(profile.IbsSubtype, profile.DiagnosisDate, profile.GetAge(utcNow), allergies),
            ResolvePilotStartDate(baseline?.CompletedAt, profile.OnboardingCompleted, profile.CreatedAt, user.CreatedAt),
            assignedNutritionist,
            baseline?.TotalScore,
            latest?.TotalScore,
            cumulativeChange,
            significantResponse);
    }

    /// <summary>
    /// Resuelve la fecha de inicio en el piloto por precedencia explícita: la línea base IBS-SSS si
    /// existe; en su defecto, la creación del perfil si el onboarding está completo; en su defecto, la
    /// creación de la cuenta.
    /// </summary>
    private static DateOnly ResolvePilotStartDate(
        DateTime? baselineCompletedAt,
        bool onboardingCompleted,
        DateTime profileCreatedAt,
        DateTime userCreatedAt)
    {
        if (baselineCompletedAt is not null)
        {
            return DateOnly.FromDateTime(baselineCompletedAt.Value);
        }

        if (onboardingCompleted)
        {
            return DateOnly.FromDateTime(profileCreatedAt);
        }

        return DateOnly.FromDateTime(userCreatedAt);
    }

    /// <summary>
    /// Enmascara un correo dejando visible solo la primera letra de la parte local y el dominio
    /// completo (por ejemplo, <c>r***@essalud.pe</c>).
    /// </summary>
    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var atIndex = email.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0)
        {
            return "***";
        }

        var firstChar = email[0];
        var domain = email[atIndex..];
        return $"{firstChar}***{domain}";
    }
}
