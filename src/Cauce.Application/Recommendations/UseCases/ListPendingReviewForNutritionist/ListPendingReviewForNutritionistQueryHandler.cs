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
/// DEC-B4-06), únicamente las excluye del resultado.
/// </summary>
public sealed class ListPendingReviewForNutritionistQueryHandler
    : IRequestHandler<ListPendingReviewForNutritionistQuery, PagedResult<RecommendationSummaryDto>>
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
    public async Task<PagedResult<RecommendationSummaryDto>> Handle(
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

        var items = page.Items.Select(RecommendationsMappings.ToSummary).ToList();
        return new PagedResult<RecommendationSummaryDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
