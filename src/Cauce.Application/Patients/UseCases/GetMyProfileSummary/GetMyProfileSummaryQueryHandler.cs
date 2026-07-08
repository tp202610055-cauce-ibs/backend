using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Dtos;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Exceptions;
using MediatR;

namespace Cauce.Application.Patients.UseCases.GetMyProfileSummary;

/// <summary>
/// Handler del perfil agregado del paciente (US28). Resuelve al paciente, su perfil clínico, el
/// nutricionista asignado y su evolución IBS-SSS (línea base, más reciente, cambio acumulado). El
/// correo se devuelve enmascarado y la fecha de inicio en el piloto se resuelve por precedencia.
/// </summary>
public sealed class GetMyProfileSummaryQueryHandler : IRequestHandler<GetMyProfileSummaryQuery, MyProfileSummaryResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IPatientProfileRepository _patientProfileRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IIbsSssAssessmentRepository _assessmentRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetMyProfileSummaryQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IPatientProfileRepository patientProfileRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IIbsSssAssessmentRepository assessmentRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _patientProfileRepository = patientProfileRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _assessmentRepository = assessmentRepository;
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
            new MyProfilePatientInfo(user.FullName, MaskEmail(user.Email)),
            new MyProfileClinicalInfo(profile.IbsSubtype, profile.DiagnosisDate, profile.GetAge(utcNow)),
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
