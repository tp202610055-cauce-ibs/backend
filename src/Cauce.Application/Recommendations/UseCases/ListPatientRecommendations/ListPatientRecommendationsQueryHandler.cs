using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Common.Models;
using Cauce.Application.Recommendations.Dtos;
using Cauce.Application.Recommendations.Mapping;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.ListPatientRecommendations;

/// <summary>
/// Handler de la consulta paginada de recomendaciones del paciente. Aplica la transición de
/// expiración on-read sobre cada recomendación de la página antes de proyectarla.
/// </summary>
public sealed class ListPatientRecommendationsQueryHandler
    : IRequestHandler<ListPatientRecommendationsQuery, PagedResult<RecommendationSummaryDto>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ListPatientRecommendationsQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IRecommendationRepository recommendationRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _recommendationRepository = recommendationRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<PagedResult<RecommendationSummaryDto>> Handle(
        ListPatientRecommendationsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var patientId = await RecommendationsUserContext
            .ResolvePatientIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var page = await _recommendationRepository
            .ListByPatientAsync(patientId, request.FilterStatus, request.Page, request.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var expiredAny = false;
        foreach (var recommendation in page.Items)
        {
            if (recommendation.IsExpired(now))
            {
                recommendation.Expire(now);
                expiredAny = true;
            }
        }

        if (expiredAny)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var items = page.Items.Select(RecommendationsMappings.ToSummary).ToList();
        return new PagedResult<RecommendationSummaryDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
