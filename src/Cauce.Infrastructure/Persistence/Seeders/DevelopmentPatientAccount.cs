using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Datos clínicos con los que se provisiona un paciente de desarrollo: perfil y puntajes de la línea base.
/// </summary>
public sealed record DevelopmentPatientProfile(
    DateOnly DateOfBirth,
    BiologicalSex Sex,
    decimal WeightKg,
    decimal HeightCm,
    IbsSubtype Subtype,
    DateOnly? DiagnosisDate,
    int PainSeverity,
    int PainFrequency,
    int BloatingSeverity,
    int BowelHabitsDissatisfaction,
    int LifeInterference);

/// <summary>
/// Provisiona la cuenta de un paciente de desarrollo lista para el Diario: identidad en Keycloak con
/// contraseña permanente, usuario verificado en el piloto activo, consentimiento vigente, perfil y línea base.
/// </summary>
public sealed class DevelopmentPatientAccount
{
    private readonly CauceDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly IPatientCodeGenerator _patientCodeGenerator;
    private readonly IConsentService _consentService;

    /// <summary>
    /// Inicializa el componente con sus dependencias.
    /// </summary>
    public DevelopmentPatientAccount(
        CauceDbContext context,
        IUserRepository userRepository,
        IKeycloakAdminClient keycloakAdminClient,
        IPatientCodeGenerator patientCodeGenerator,
        IConsentService consentService)
    {
        _context = context;
        _userRepository = userRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _patientCodeGenerator = patientCodeGenerator;
        _consentService = consentService;
    }

    /// <summary>
    /// Crea la identidad en Keycloak y agrega al contexto la cuenta local, sin guardar: el llamador decide
    /// cuándo persistir. Reutiliza el usuario de Keycloak si un intento previo lo dejó creado.
    /// </summary>
    /// <param name="email">Correo ya normalizado.</param>
    /// <param name="fullName">Nombre completo.</param>
    /// <param name="password">Contraseña permanente; nunca se registra en logs.</param>
    /// <param name="profile">Perfil clínico y puntajes de la línea base.</param>
    /// <param name="enrolledAtUtc">Momento del consentimiento y de la creación del perfil.</param>
    /// <param name="baselineAtUtc">Momento de la línea base y del fin del onboarding.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El usuario local agregado al contexto.</returns>
    /// <exception cref="Cauce.Application.Common.Exceptions.KeycloakIntegrationException">
    /// Si Keycloak rechaza la creación o la contraseña.
    /// </exception>
    public async Task<User> ProvisionAsync(
        string email,
        string fullName,
        string password,
        DevelopmentPatientProfile profile,
        DateTime enrolledAtUtc,
        DateTime baselineAtUtc,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);

        var existing = await _keycloakAdminClient.FindByEmailAsync(email, ct).ConfigureAwait(false);
        var keycloakId = existing?.Id
            ?? await _keycloakAdminClient
                .CreateUserAsync(email, fullName, UserRoles.Patient, requireEmailVerification: false, ct)
                .ConfigureAwait(false);
        await _keycloakAdminClient.ResetPasswordAsync(keycloakId, password, ct).ConfigureAwait(false);

        var patientCode = await _patientCodeGenerator.NextAsync(ct).ConfigureAwait(false);
        var user = User.CreatePatient(Guid.NewGuid(), keycloakId, email, fullName, patientRoleId, patientCode);
        user.VerifyEmail();
        user.EnrollInActivePilot();
        await _userRepository.AddAsync(user, ct).ConfigureAwait(false);

        // El hash es el del documento vigente, el mismo que exige el registro real.
        _context.Add(ConsentRecord.Capture(
            Guid.NewGuid(),
            user.Id,
            _consentService.GetCurrentVersion(),
            _consentService.GetCurrentTextHash(),
            "127.0.0.1",
            enrolledAtUtc));

        var patientProfile = PatientProfile.Create(
            Guid.NewGuid(),
            user.Id,
            profile.DateOfBirth,
            profile.Sex,
            profile.WeightKg,
            profile.HeightCm,
            profile.Subtype,
            profile.DiagnosisDate,
            medications: null,
            enrolledAtUtc);
        patientProfile.CompleteOnboarding(baselineAtUtc);
        _context.Add(patientProfile);

        _context.Add(IbsSssAssessment.Submit(
            Guid.NewGuid(), user.Id, AssessmentType.Baseline, cycleNumber: 0,
            profile.PainSeverity, profile.PainFrequency, profile.BloatingSeverity,
            profile.BowelHabitsDissatisfaction, profile.LifeInterference, baselineAtUtc));

        return user;
    }
}
