using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Common.Models;
using Cauce.Application.Recommendations.Dtos;
using Cauce.Application.Recommendations.Mapping;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.ListPendingReviewForNutritionist;

/// <summary>
/// Handler de la consulta de recomendaciones pendientes de revisión del nutricionista. Es de
/// solo lectura: no transita las expiradas (responsabilidad del worker del Prompt 5; ver
/// DEC-B4-06), únicamente las excluye del resultado. Cada fila lleva el paciente al que pertenece,
/// resuelto en una sola consulta para toda la página (acta A69).
/// </summary>
public sealed class ListPendingReviewForNutritionistQueryHandler
    : IRequestHandler<ListPendingReviewForNutritionistQuery, PagedResult<PendingReviewRecommendationDto>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ListPendingReviewForNutritionistQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IRecommendationRepository recommendationRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _recommendationRepository = recommendationRepository;
    }

    /// <inheritdoc />
    public async Task<PagedResult<PendingReviewRecommendationDto>> Handle(
        ListPendingReviewForNutritionistQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var nutritionistId = await RecommendationsUserContext
            .ResolveNutritionistIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var page = await _recommendationRepository
            .ListPendingReviewByNutritionistAsync(nutritionistId, request.Page, request.PageSize, now, cancellationToken)
            .ConfigureAwait(false);

        var patientIds = page.Items.Select(recommendation => recommendation.PatientId).Distinct().ToList();
        var patientNames = await _userRepository
            .GetFullNamesAsync(patientIds, cancellationToken)
            .ConfigureAwait(false);

        var items = page.Items
            .Select(recommendation => RecommendationsMappings.ToPendingReview(
                recommendation,
                patientNames.GetValueOrDefault(recommendation.PatientId, string.Empty)))
            .ToList();
        return new PagedResult<PendingReviewRecommendationDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
