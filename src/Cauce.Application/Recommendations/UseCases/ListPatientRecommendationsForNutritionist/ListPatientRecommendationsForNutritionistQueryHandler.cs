using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Common.Models;
using Cauce.Application.Patients.Services;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Application.Recommendations.Dtos;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.ListPatientRecommendationsForNutritionist;

/// <summary>
/// Handler de <see cref="ListPatientRecommendationsForNutritionistQuery"/>. Solo lee: no transita las
/// vencidas, y registra la lectura en la bitácora.
/// </summary>
public sealed class ListPatientRecommendationsForNutritionistQueryHandler
    : IRequestHandler<ListPatientRecommendationsForNutritionistQuery, PagedResult<PatientRecommendationForNutritionistDto>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly IFoodCatalogReader _foodCatalogReader;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ListPatientRecommendationsForNutritionistQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IRecommendationRepository recommendationRepository,
        IFoodCatalogReader foodCatalogReader,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _recommendationRepository = recommendationRepository;
        _foodCatalogReader = foodCatalogReader;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<PagedResult<PatientRecommendationForNutritionistDto>> Handle(
        ListPatientRecommendationsForNutritionistQuery request,
        CancellationToken cancellationToken)
    {
        await NutritionistPatientRecordAccess
            .EnsureActiveAssignmentAsync(
                _currentUserService, _userRepository, _nutritionistPatientRepository, request.PatientUserId, cancellationToken)
            .ConfigureAwait(false);

        var page = await _recommendationRepository
            .ListAllByPatientAsync(request.PatientUserId, request.Page, request.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var foodIds = page.Items
            .SelectMany(recommendation => recommendation.Items)
            .SelectMany(item => item.SubstituteFoodId is { } substitute ? new[] { item.FoodId, substitute } : [item.FoodId])
            .Distinct()
            .ToList();
        IReadOnlyDictionary<Guid, FoodNameInfo> foodNames = foodIds.Count == 0
            ? new Dictionary<Guid, FoodNameInfo>()
            : await _foodCatalogReader.GetFoodNamesAsync(foodIds, cancellationToken).ConfigureAwait(false);

        await NutritionistPatientRecordAccess
            .RecordViewAsync(_auditLogger, _unitOfWork, request.PatientUserId, "recommendations", cancellationToken)
            .ConfigureAwait(false);

        var items = page.Items
            .Select(recommendation => new PatientRecommendationForNutritionistDto(
                recommendation.Id,
                recommendation.Status,
                recommendation.Source,
                recommendation.Title,
                recommendation.Items.Count,
                recommendation.GeneratedAt,
                recommendation.ReviewedAt,
                recommendation.ExpiresAt,
                recommendation.ValidUntil,
                recommendation.IsActive,
                recommendation.ArchivedAt,
                recommendation.ArchiveReason,
                recommendation.Items
                    .Select(item => new RecommendationFoodPreviewDto(
                        foodNames.GetValueOrDefault(item.FoodId)?.Name ?? string.Empty,
                        item.ActionType,
                        item.SubstituteFoodId is { } substitute ? foodNames.GetValueOrDefault(substitute)?.Name : null))
                    .ToList()))
            .ToList();

        return new PagedResult<PatientRecommendationForNutritionistDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
