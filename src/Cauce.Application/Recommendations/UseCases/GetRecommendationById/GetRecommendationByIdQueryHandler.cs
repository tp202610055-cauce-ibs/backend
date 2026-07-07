using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Dtos;
using Cauce.Application.Recommendations.Mapping;
using Cauce.Domain.Identity;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Exceptions;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.GetRecommendationById;

/// <summary>
/// Handler de la consulta de detalle de una recomendación. Resuelve la autorización por rol,
/// aplica la transición de expiración on-read y enriquece el detalle con los nombres de los
/// alimentos y de la versión de modelo.
/// </summary>
public sealed class GetRecommendationByIdQueryHandler : IRequestHandler<GetRecommendationByIdQuery, RecommendationDetailDto>
{
    private const int AnalysisWindowDays = 14;
    private const int CorrelationWindowHours = 4;

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IModelVersionRepository _modelVersionRepository;
    private readonly IFoodCatalogReader _foodCatalogReader;
    private readonly IRecommendationSupportingDataReader _supportingDataReader;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetRecommendationByIdQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IRecommendationRepository recommendationRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IModelVersionRepository modelVersionRepository,
        IFoodCatalogReader foodCatalogReader,
        IRecommendationSupportingDataReader supportingDataReader,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _recommendationRepository = recommendationRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _modelVersionRepository = modelVersionRepository;
        _foodCatalogReader = foodCatalogReader;
        _supportingDataReader = supportingDataReader;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<RecommendationDetailDto> Handle(GetRecommendationByIdQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var recommendation = await _recommendationRepository
            .GetByIdWithDetailsAsync(request.RecommendationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new RecommendationNotFoundException(request.RecommendationId);

        var isPatient = await EnsureAuthorizedAsync(recommendation, cancellationToken).ConfigureAwait(false);

        if (recommendation.IsExpired(now))
        {
            recommendation.Expire(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // US14 CA03: el paciente no puede ver recomendaciones archivadas ni en estados no visibles;
        // se responde 404 para no revelar su existencia (acta A24). El nutricionista sí las ve.
        if (isPatient && !recommendation.IsVisibleToPatient())
        {
            throw new RecommendationNotFoundException(request.RecommendationId);
        }

        var modelVersion = recommendation.ModelVersionId is { } modelVersionId
            ? await _modelVersionRepository.GetByIdAsync(modelVersionId, cancellationToken).ConfigureAwait(false)
            : null;

        var foodIds = recommendation.Items
            .SelectMany(item => item.SubstituteFoodId.HasValue
                ? new[] { item.FoodId, item.SubstituteFoodId.Value }
                : new[] { item.FoodId })
            .Distinct()
            .ToList();

        var foodNames = await _foodCatalogReader.GetFoodNamesAsync(foodIds, cancellationToken).ConfigureAwait(false);

        // Bloque 2 (US15 CA02): nombre completo del nutricionista revisor.
        string? reviewedByNutritionistName = null;
        if (recommendation.ReviewedByNutritionistId is { } reviewerId)
        {
            var reviewer = await _userRepository.FindByIdAsync(reviewerId, cancellationToken).ConfigureAwait(false);
            reviewedByNutritionistName = reviewer?.FullName;
        }

        // Bloque 4 (US15 CA03): cifras de respaldo sobre la ventana de análisis de 14 días.
        var windowTo = now;
        var windowFrom = windowTo.AddDays(-AnalysisWindowDays);
        var supporting = await _supportingDataReader
            .GetAsync(recommendation.PatientId, windowFrom, windowTo, cancellationToken)
            .ConfigureAwait(false);
        var supportingData = new RecommendationSupportingDataDto(
            supporting.SymptomCount,
            supporting.MealCount,
            supporting.TopHighFodmapFoods,
            CorrelationWindowHours,
            windowFrom,
            windowTo);

        return RecommendationsMappings.ToDetail(
            recommendation, modelVersion?.VersionName ?? string.Empty, foodNames, reviewedByNutritionistName, supportingData);
    }

    private async Task<bool> EnsureAuthorizedAsync(Recommendation recommendation, CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);
        if (user.RoleId == patientRoleId)
        {
            if (recommendation.PatientId != user.Id)
            {
                throw new RecommendationAccessDeniedException();
            }

            return true;
        }

        var nutritionistRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, ct).ConfigureAwait(false);
        if (user.RoleId == nutritionistRoleId)
        {
            var isAssigned = await _nutritionistPatientRepository
                .ActiveAssignmentExistsAsync(user.Id, recommendation.PatientId, ct)
                .ConfigureAwait(false);
            if (!isAssigned)
            {
                throw new RecommendationAccessDeniedException();
            }

            return false;
        }

        throw new RecommendationAccessDeniedException();
    }
}
