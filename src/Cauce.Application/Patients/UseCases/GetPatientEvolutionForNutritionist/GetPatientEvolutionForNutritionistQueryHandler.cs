using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Exceptions;
using MediatR;

namespace Cauce.Application.Patients.UseCases.GetPatientEvolutionForNutritionist;

/// <summary>
/// Handler de las métricas de evolución de un paciente para el nutricionista (US21).
/// Verifica la asignación activa (403 si no existe), reconstruye la serie IBS-SSS y
/// calcula la variación respecto de la línea base y la frecuencia de registro reciente.
/// </summary>
public sealed class GetPatientEvolutionForNutritionistQueryHandler
    : IRequestHandler<GetPatientEvolutionForNutritionistQuery, PatientEvolutionForNutritionistResult>
{
    private const int RegistrationWindowDays = 14;

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IIbsSssAssessmentRepository _assessmentRepository;
    private readonly IRecommendationSupportingDataReader _supportingDataReader;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetPatientEvolutionForNutritionistQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IIbsSssAssessmentRepository assessmentRepository,
        IRecommendationSupportingDataReader supportingDataReader)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _assessmentRepository = assessmentRepository;
        _supportingDataReader = supportingDataReader;
    }

    /// <inheritdoc />
    public async Task<PatientEvolutionForNutritionistResult> Handle(
        GetPatientEvolutionForNutritionistQuery request,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var nutritionist = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var nutritionistRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, cancellationToken).ConfigureAwait(false);
        if (nutritionist.RoleId != nutritionistRoleId)
        {
            throw new UnauthorizedAccessException("Solo un nutricionista puede consultar la evolución de un paciente.");
        }

        var hasAccess = await _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(nutritionist.Id, request.PatientUserId, cancellationToken)
            .ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new PatientAccessNotAuthorizedException();
        }

        var assessments = await _assessmentRepository
            .ListByPatientAsync(request.PatientUserId, cancellationToken)
            .ConfigureAwait(false);

        var baseline = assessments.FirstOrDefault(a => a.AssessmentType == AssessmentType.Baseline);
        var latest = assessments.Count > 0 ? assessments[^1] : null;

        var timeline = assessments
            .Select(assessment => new IbsSssEvolutionEntry(
                ClinicalRegistryMappings.ToSummary(assessment),
                ComputeDelta(assessment, baseline)))
            .ToList();

        var registrationWindowStart = utcNow.AddDays(-RegistrationWindowDays);
        var supporting = await _supportingDataReader
            .GetAsync(request.PatientUserId, registrationWindowStart, utcNow, cancellationToken)
            .ConfigureAwait(false);

        return new PatientEvolutionForNutritionistResult(
            timeline,
            baseline?.TotalScore,
            latest?.TotalScore,
            ComputePercentChange(baseline, latest),
            baseline is not null && latest is not null && latest.IsClinicallySignificantImprovement(baseline),
            supporting.SymptomCount + supporting.MealCount);
    }

    private static int? ComputeDelta(IbsSssAssessment assessment, IbsSssAssessment? baseline)
    {
        if (baseline is null || assessment.AssessmentType == AssessmentType.Baseline)
        {
            return null;
        }

        return assessment.CompareTotalScoreTo(baseline);
    }

    private static decimal? ComputePercentChange(IbsSssAssessment? baseline, IbsSssAssessment? latest)
    {
        if (baseline is null || latest is null || baseline.TotalScore == 0)
        {
            return null;
        }

        return Math.Round((decimal)(latest.TotalScore - baseline.TotalScore) / baseline.TotalScore * 100m, 2);
    }
}
